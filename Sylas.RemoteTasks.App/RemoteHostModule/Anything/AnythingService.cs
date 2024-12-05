using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Constants;
using Sylas.RemoteTasks.Utils.Dto;
using Sylas.RemoteTasks.Utils.Template;
using System.Net;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// 操作业务数据(自定义命令的配置集)
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="executorRepository"></param>
    /// <param name="memoryCache"></param>
    public class AnythingService(RepositoryBase<AnythingSetting> repository, RepositoryBase<AnythingExecutor> executorRepository, RepositoryBase<AnythingCommand> commandRepository, IMemoryCache memoryCache)
    {
        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="search">分页查询参数</param>
        /// <returns></returns>
        public async Task<PagedData<AnythingSetting>> GetAnythingSettingsAsync(DataSearch? search = null)
        {
            search ??= new();
            var recordPage = await repository.GetPageAsync(search);
            return recordPage;
        }

        /// <summary>
        /// 根据Id查询
        /// </summary>
        /// <param name="search">分页查询参数</param>
        /// <returns></returns>
        public async Task<AnythingSetting?> GetAnythingSettingByIdAsync(int id)
        {
            var anythingSetting = await repository.GetByIdAsync(id);
            return anythingSetting;
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> AddAnythingSettingAsync(AnythingSetting anythingSetting)
        {
            var added = await repository.AddAsync(anythingSetting);
            if (!string.IsNullOrWhiteSpace(anythingSetting.Commands))
            {
                var commands = JsonConvert.DeserializeObject<List<CommandInfo>>(anythingSetting.Commands);
                if (commands is not null)
                {
                    foreach (var command in commands)
                    {
                        await _commandRepository.AddAsync(command.ToCommandEntity(anythingSetting.Id));
                    }
                }
            }
            return added > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }

        /// <summary>
        /// 通过Id删除记录
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> DeleteAnythingSettingByIdAsync(int id)
        {
            var added = await repository.DeleteAsync(id);
            
            var pageData = await _commandRepository.GetPageAsync(new DataSearch(1, 10, new DataFilter() { FilterItems = [new(nameof(AnythingCommand.AnythingId), "=", id)] }));
            var commandIds = pageData.Data.Select(x => x.Id);
            foreach (var commandId in commandIds)
            {
                await _commandRepository.DeleteAsync(commandId);
            }

            return added > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> UpdateAnythingSettingAsync(Dictionary<string, string> anythingSetting)
        {
            var added = await repository.UpdateAsync(anythingSetting);
            return added > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }

        const string _cacheKeyAllAnythingInfos = "AllAnythingInfos";
        private readonly RepositoryBase<AnythingCommand> _commandRepository = commandRepository;

        /// <summary>
        /// 获取所有的AnythingInfo, 用于展示操作对象信息和相应的可执行命令
        /// </summary>
        /// <returns></returns>
        public async Task<List<AnythingInfo>> GetAllAnythingInfosAsync()
        {
            if (memoryCache.TryGetValue(_cacheKeyAllAnythingInfos, out List<AnythingInfo>? anythingInfos) && anythingInfos is not null)
            {
                return anythingInfos;
            }
            anythingInfos = [];
            var anythingSettingsPage = await GetAnythingSettingsAsync(new DataSearch(1, 1000));
            if (anythingSettingsPage.Data is not null)
            {
                foreach (var anythingSetting in anythingSettingsPage.Data)
                {
                    var anythingInfo = await BuildAnythingInfoAsync(anythingSetting);
                    anythingInfos.Add(anythingInfo);
                }
            }
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(60 * 8));
            memoryCache.Set(_cacheKeyAllAnythingInfos, anythingInfos, cacheEntryOptions);
            return anythingInfos;
        }
        /// <summary>
        /// 获取命令执行器列表
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public async Task<PagedData<AnythingExecutor>> ExecutorsAsync(DataSearch search)
        {
            var pageData = await executorRepository.GetPageAsync(search);
            return pageData;
        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<CommandResult> ExecuteAsync(CommandInfoInDto dto)
        {
            var anythingInfo = await GetAnythingInfoBySettingAndCommandAsync(dto.SettingId);
            var commandInfo = anythingInfo.Commands.FirstOrDefault(x => x.Name == dto.CommandName) ?? throw new Exception($"未知的命令{dto.CommandName}");
            if (anythingInfo.CommandExecutor is null)
            {
                throw new Exception("Anything Executor Error");
            }
            if (!string.IsNullOrWhiteSpace(commandInfo.Domain) && commandInfo.Domain != Dns.GetHostName())
            {
                string requestId = Guid.NewGuid().ToString();
                CommandTasks.Enqueue(new CommandInfoTaskDto { RequestId = requestId, Domain = commandInfo.Domain, SettingId = dto.SettingId, CommandName = dto.CommandName });
                for (int i = 0; i < 100; i++)
                {
                    if (RemoteCommandResults.TryGetValue(requestId, out CommandResult? value))
                    {
                        LoggerHelper.LogCritical($"AnythingService: 获取到命令执行的结果:{JsonConvert.SerializeObject(value)}");
                        RemoteCommandResults.Remove(requestId);
                        return value;
                    }
                    await Task.Delay(1000);
                    if ((i + 1) % 10 == 0)
                    {
                        LoggerHelper.LogInformation("AnythingService: 等待命令返回结果...");
                    }
                }
                return new CommandResult(false, "执行时间过长, 请稍后查看执行结果");
            }
            CommandResult cr = await anythingInfo.CommandExecutor.ExecuteAsync(commandInfo.CommandTxt);
            return cr;
        }
        public static readonly Queue<CommandInfoTaskDto> CommandTasks = new();
        public static readonly Dictionary<string, CommandResult> RemoteCommandResults = [];
        /// <summary>
        /// 根据settingId和commandName获取对应的AnythingInfo
        /// </summary>
        /// <param name="settingId"></param>
        /// <param name="commandName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<AnythingInfo> GetAnythingInfoBySettingAndCommandAsync(int settingId)
        {
            if (memoryCache.TryGetValue(_cacheKeyAllAnythingInfos, out List<AnythingInfo>? anythingInfos) && anythingInfos is not null)
            {
                var anything = anythingInfos.FirstOrDefault(x => x.SettingId == settingId);
                return anything ?? throw new Exception("无效的Anything");
            }
            var anythingSetting = (await GetAnythingSettingByIdAsync(settingId)) ?? throw new Exception($"无效的AnythingSetting");
            var anythingInfo = await BuildAnythingInfoAsync(anythingSetting);
            return anythingInfo;
        }
        /// <summary>
        /// 从AnythingSetting解析一个AnythingInfo对象
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<AnythingInfo> BuildAnythingInfoAsync(AnythingSetting anythingSetting)
        {
            #region 解析Properties, 添加Name和Title
            var properties = GetAllProperties(anythingSetting);
            #endregion

            #region 解析Executor对象和构造函数参数(使用Properties解析参数)
            AnythingExecutor? anythingExecutor;
            if (anythingSetting.Executor == 0)
            {
                anythingExecutor = new AnythingExecutor { Name = "SystemCmd" };
            }
            else
            {
                // 在查询Anything列表时, 多个AnythingInfo都可能对应一个Executor, 所以此查询在一瞬间可能很频繁, 所以添加了缓存
                string cacheKey = $"CacheKeyExecutor_{anythingSetting.Executor}";
                if (!memoryCache.TryGetValue(cacheKey, out anythingExecutor) || anythingExecutor is null)
                {
                    anythingExecutor = await executorRepository.GetByIdAsync(anythingSetting.Executor) ?? throw new Exception($"无效的AnythingExecutor: {anythingSetting.Executor}");
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(1));
                    memoryCache.Set(cacheKey, anythingExecutor, cacheEntryOptions);
                }
            }

            var executorName = anythingExecutor.Name;
            var argTmpls = string.IsNullOrWhiteSpace(anythingExecutor.Arguments)
                ? []
                : JsonConvert.DeserializeObject<List<ArgumentInfo>>(anythingExecutor.Arguments) ?? throw new Exception($"无效的AnythingExecutor参数:{anythingExecutor.Arguments}");
            object[] args = new object[argTmpls.Count];
            for (int i = 0; i < args.Length; i++)
            {
                var argTmpl = argTmpls[i];
                args[i] = TmplHelper.ResolveExpressionValue(argTmpl.ArgumentValue, properties);
                if (!string.IsNullOrWhiteSpace(argTmpl.ArgumentType) && !args[i].GetType().Name.Equals(argTmpl.ArgumentType, StringComparison.CurrentCultureIgnoreCase))
                {
                    switch (argTmpl.ArgumentType.ToLower())
                    {
                        case "int32":
                            args[i] = Convert.ToInt32(args[i]);
                            break;
                        case "int64":
                            args[i] = Convert.ToInt64(args[i]);
                            break;
                        default:
                            break;
                    }
                }
            }

            var result = ICommandExecutor.Create(executorName, args);
            if (result.Code != 1)
            {
                throw new Exception(result.ErrMsg);
            }
            var anythingCommandExecutor = result.Data ?? throw new Exception("无法解析命令执行器");
            #endregion

            #region 解析出AnythingInfo
            // 解析CommandTxt中的模板
            var commands = JsonConvert.DeserializeObject<List<CommandInfo>>(anythingSetting.Commands) ?? [];
            foreach (var anythingCommand in commands)
            {
                anythingCommand.CommandTxt = TmplHelper.ResolveExpressionValue(anythingCommand.CommandTxt, properties)?.ToString() ?? throw new Exception($"解析命令\"{anythingCommand.CommandTxt}\"异常");
                if (!string.IsNullOrWhiteSpace(anythingCommand.ExecutedState))
                {
                    var start = DateTime.Now;
                    anythingCommand.ExecutedState = (await anythingCommandExecutor.ExecuteAsync(anythingCommand.ExecutedState)).Message;
                    Console.WriteLine($"获取命令状态耗时: {(DateTime.Now - start).TotalMilliseconds}/ms");
                }
            }

            var anythingInfo = new AnythingInfo()
            {
                SettingId = anythingSetting.Id,
                CommandExecutor = anythingCommandExecutor,
                Name = anythingSetting.Name,
                Title = anythingSetting.Title,
                Properties = properties,
                Commands = commands
            };
            #endregion

            return anythingInfo;
        }
        /// <summary>
        /// 获取模板变量Properties
        /// </summary>
        /// <param name="anythingSetting"></param>

        public Dictionary<string, object> GetAllProperties(AnythingSetting anythingSetting)
        {
            var properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(anythingSetting.Properties) ?? [];
            properties.ResolveSelfTmplValues();
            if (!properties.ContainsKey(nameof(anythingSetting.Name)))
            {
                properties[nameof(anythingSetting.Name)] = anythingSetting.Name;
            }
            if (!properties.ContainsKey(nameof(anythingSetting.Title)))
            {
                properties[nameof(anythingSetting.Title)] = anythingSetting.Title;
            }
            
            if (!properties.ContainsKey(nameof(PathConstants.DefaultSshPrivateKeyFileEd25519)))
            {
                properties[nameof(PathConstants.DefaultSshPrivateKeyFileEd25519)] = PathConstants.DefaultSshPrivateKeyFileEd25519;
            }
            if (!properties.ContainsKey(nameof(PathConstants.DefaultSshPrivateKeyFileRsa)))
            {
                properties[nameof(PathConstants.DefaultSshPrivateKeyFileRsa)] = PathConstants.DefaultSshPrivateKeyFileRsa;
            }
            return properties;
        }

        /// <summary>
        /// 解析一个命令配置
        /// </summary>
        /// <param name="settingId"></param>
        /// <param name="commandSetting"></param>
        /// <returns></returns>
        public async Task<RequestResult<string>> ResolveCommandSettingAsync(CommandResolveDto dto)
        {
            var anythingSetting = (await GetAnythingSettingByIdAsync(dto.Id));
            if (anythingSetting is null)
            {
                return RequestResult<string>.Error($"未找到id为{dto.Id}的操作对象");
            }
            var properties = GetAllProperties(anythingSetting);
            var commandResolved = TmplHelper.ResolveExpressionValue(dto.CmdTxt, properties)?.ToString();
            if (string.IsNullOrWhiteSpace(commandResolved))
            {
                return RequestResult<string>.Error($"解析命令[{dto.CmdTxt}]异常");
            }
            return RequestResult<string>.Success(commandResolved);
        }
    }
}

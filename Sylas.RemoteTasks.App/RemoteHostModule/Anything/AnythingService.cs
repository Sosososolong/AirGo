using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Constants;
using Sylas.RemoteTasks.Utils.Dtos;
using Sylas.RemoteTasks.Utils.Template;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// 操作业务数据(自定义命令的配置集)
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="executorRepository"></param>
    /// <param name="commandRepository"></param>
    /// <param name="logger"></param>
    /// <param name="memoryCache"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="httpContextAccessor"></param>
    public class AnythingService(
        RepositoryBase<AnythingSetting> repository,
        RepositoryBase<AnythingExecutor> executorRepository,
        RepositoryBase<AnythingCommand> commandRepository,
        ILogger<AnythingService> logger,
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor)
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
        /// 根据Id查询Anything配置包括命令明细
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<AnythingSettingDetails> GetAnythingSettingDetailsByIdAsync(int id)
        {
            var anythingSetting = await GetAnythingSettingByIdAsync(id) ?? throw new Exception($"配置{id}不存在");
            var commandsPage = await _commandRepository.GetPageAsync(
                new DataSearch(1, 1000, new DataFilter { FilterItems = [new FilterItem(nameof(AnythingCommand.AnythingId), "=", id)] })
            );
            var details = anythingSetting.ToDetails(commandsPage.Data);
            return details;
        }

        /// <summary>
        /// 添加Anything
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> AddAnythingSettingAsync(AnythingSetting anythingSetting)
        {
            var added = await repository.AddAsync(anythingSetting);
            return added > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }

        /// <summary>
        /// 通过Id删除记录
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> DeleteAnythingSettingByIdAsync(int id)
        {
            var affectedRows = await repository.DeleteAsync(id);

            var commands = await GetAnythingCommandsAsync(id);
            var commandIds = commands.Select(x => x.Id);
            foreach (var commandId in commandIds)
            {
                await _commandRepository.DeleteAsync(commandId);
            }

            return affectedRows > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }
        /// <summary>
        /// 通过Id删除命令记录
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> DeleteAnythingCommandByIdAsync(int id)
        {
            var command = await _commandRepository.GetByIdAsync(id);
            if (command is null)
            {
                return new OperationResult(false, "命令不存在");
            }
            var affectedRows = await _commandRepository.DeleteAsync(id);
            if (affectedRows > 0)
            {
                var anythingSetting = await repository.GetByIdAsync(command.AnythingId);
                if (anythingSetting is not null)
                {
                    string cacheKeyAnythingInfo = AnythingInfoCacheKey(anythingSetting.Id);
                    if (memoryCache.TryGetValue(cacheKeyAnythingInfo, out AnythingInfo? anythingInfo))
                    {
                        if (anythingInfo is not null)
                        {
                            var deletedCommand = anythingInfo.Commands.FirstOrDefault(x => x.Id == id);
                            if (deletedCommand is not null)
                            {
                                var commands = anythingInfo.Commands.ToList();
                                commands.Remove(deletedCommand);
                                anythingInfo.Commands = commands;
                            }
                        }
                    }
                }
                return new OperationResult(true);
            }
            return new OperationResult(false, "Affected rows: 0");
        }

        /// <summary>
        /// 获取Anything的命令列表
        /// </summary>
        /// <param name="anythingId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<AnythingCommand>> GetAnythingCommandsAsync(int anythingId)
        {
            var pageData = await _commandRepository.GetPageAsync(
                new DataSearch(
                    1,
                    1000,
                    new DataFilter()
                    {
                        FilterItems = [
                            new(nameof(AnythingCommand.AnythingId), "=", anythingId)
                        ]
                    }
                )
            );
            return pageData.Data;
        }
        /// <summary>
        /// 为Anything配置添加一条命令
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<OperationResult> AddCommandAsync(AnythingCommand command)
        {
            var commandId = await _commandRepository.AddAsync(command);
            if (commandId > 0)
            {
                command.Id = commandId;
                var anythingSetting = await repository.GetByIdAsync(command.AnythingId);
                if (anythingSetting is not null)
                {
                    string cacheKeyAnythingInfo = AnythingInfoCacheKey(anythingSetting.Id);
                    if (memoryCache.TryGetValue(cacheKeyAnythingInfo, out AnythingInfo? anythingInfo))
                    {
                        if (anythingInfo is not null)
                        {
                            var commands = anythingInfo.Commands.ToList();
                            commands.Add(command);
                            anythingInfo.Commands = commands;
                        }
                    }
                }
                return new OperationResult(true);
            }
            return new OperationResult(commandId > 0);
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> UpdateAnythingSettingAsync(Dictionary<string, string> anythingSetting)
        {
            var affectedRows = await repository.UpdateAsync(anythingSetting);
            return affectedRows > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }
        /// <summary>
        /// 更新命令
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<OperationResult> UpdateCommandAsync(Dictionary<string, string> command)
        {
            if (!command.TryGetValue("id", out string? id) || id is null)
            {
                return new OperationResult(false, "命令id不能为空");
            }
            
            var affectedRows = await _commandRepository.UpdateAsync(command);
            if (affectedRows > 0)
            {
                var entity = await _commandRepository.GetByIdAsync(Convert.ToInt32(id));
                if (entity is null)
                {
                    return new OperationResult(false, $"命令{id}不存在");
                }
                string cacheKeyAnythingInfo = AnythingInfoCacheKey(entity.AnythingId);
                if (memoryCache.TryGetValue(cacheKeyAnythingInfo, out AnythingInfo? anythingInfo))
                {
                    if (anythingInfo is not null)
                    {
                        var commands = anythingInfo.Commands.ToList();
                        var exist = commands.FirstOrDefault(x => x.Id == entity.Id);
                        if (exist is not null)
                        {
                            commands.Remove(exist);
                        }
                        commands.Add(entity);
                        anythingInfo.Commands = commands;
                    }
                }
            }
            return new OperationResult(affectedRows > 0);
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
            var allAnythingSettingsPage = await GetAnythingSettingsAsync(new DataSearch(1, 10000));
            var allCmdsPage = await _commandRepository.GetPageAsync(new DataSearch(1, 100000));
            if (allAnythingSettingsPage.Data is not null)
            {
                foreach (var anythingSetting in allAnythingSettingsPage.Data)
                {
                    var cmds = allCmdsPage.Data.Where(x => x.AnythingId == anythingSetting.Id);
                    var anythingSettingDetails = anythingSetting.ToDetails(cmds);
                    var anythingInfo = await BuildAnythingInfoAsync(anythingSettingDetails);
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
            var commandInfo = await _commandRepository.GetByIdAsync(dto.CommandId) ?? throw new Exception($"未知的命令{dto.CommandId}");
            var anythingInfo = await GetAnythingInfoBySettingIdAsync(commandInfo.AnythingId);
            if (anythingInfo.CommandExecutor is null)
            {
                throw new Exception("Anything Executor Error");
            }
            string commandTaskNo = string.IsNullOrWhiteSpace(dto.CommandExecuteNo) ? Guid.NewGuid().ToString() : dto.CommandExecuteNo;
            if (!string.IsNullOrWhiteSpace(commandInfo.Domain) && commandInfo.Domain != AppStatus.Domain)
            {
                if (AppStatus.IsCenterServer)
                {
                    var commandTaskDto = new CommandInfoTaskDto {
                        CommandId = dto.CommandId,
                        CommandExecuteNo = commandTaskNo,
                        Domain = commandInfo.Domain,
                        SettingId = dto.CommandId,
                        CommandName = commandInfo.Name
                    };

                    #region 将任务信息添加到domain对应队列中
                    if (!_serverNodeQueues.TryGetValue(commandInfo.Domain, out Queue<CommandInfoTaskDto>? cmdTasks) || cmdTasks is null)
                    {
                        cmdTasks = new Queue<CommandInfoTaskDto>();
                        _serverNodeQueues[commandInfo.Domain] = cmdTasks;
                        logger.LogInformation("AnythingService ExecuteAsync: 添加命令任务到队列[{domain}]中", commandInfo.Domain);
                    }
                    cmdTasks.Enqueue(commandTaskDto);
                    #endregion

                    return await GetCommandResultAsync(commandTaskNo);
                }
                else
                {
                    using var client = httpClientFactory.CreateClient();
                    string? accessToken = string.Empty;
                    if (httpContextAccessor.HttpContext!.Request.Headers.TryGetValue("Authorization", out var token))
                    {
                        accessToken = token;
                    }
                    else
                    {
                        accessToken = (await httpContextAccessor.HttpContext!.AuthenticateAsync()).Properties?.GetString(".Token.access_token");
                    }

                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    }

                    string mediaType = "application/json";
                    string bodyContent = JsonConvert.SerializeObject(dto);
                    HttpContent parameters = new StringContent(bodyContent, Encoding.UTF8, mediaType);
                    var response = await client.PostAsJsonAsync($"{AppStatus.CenterWebServer!.TrimEnd('/')}/Hosts/ExecuteCommand", dto);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return new CommandResult(false, $"{commandInfo.Domain}命令需要转到中心服务器, 请求失败: {response.ReasonPhrase}");
                    }
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (!responseContent.StartsWith('{'))
                    {
                        return new CommandResult(false, $"{commandInfo.Domain}命令需要转到中心服务器, 授权失败");
                    }
                    RequestResult<CommandResult>? requestResult = JsonConvert.DeserializeObject<RequestResult<CommandResult>>(responseContent);
                    if (requestResult is null)
                    {
                        return new CommandResult(false, "请求中心服务器失败");
                    }
                    return requestResult.Data ?? new CommandResult(false, "中心服务器没有返回命令执行结果");
                }
            }
            var resolvedResult = await ResolveCommandSettingAsync(new CommandResolveDto() { Id = commandInfo.AnythingId, CmdTxt = commandInfo.CommandTxt });
            string resolvedCommandContent = resolvedResult.Data ?? string.Empty;
            CommandResult cr = await AnythingIdAndCommandExecutorMap[anythingInfo.SettingId]([resolvedCommandContent]);
            if (!string.IsNullOrWhiteSpace(dto.CommandExecuteNo))
            {
                cr.CommandExecuteNo = dto.CommandExecuteNo;
            }
            return cr;
        }
        static readonly Dictionary<string, CommandResult> _remoteCommandResults = [];
        static readonly Dictionary<string, Queue<CommandInfoTaskDto>> _serverNodeQueues = [];

        /// <summary>
        /// 获取命令任务
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<CommandInfoTaskDto?> GetCommandTaskAsync(string domain, CancellationToken cancellationToken = default)
        {
            int i = 0;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LoggerHelper.LogCritical("AnythingService GetCommandTaskAsync: 任务取消, 停止检查队列!");
                    return null;
                }
                i++;
                bool writeLog = i % 3600 == 0;
                if (writeLog)
                {
                    LoggerHelper.LogInformation($"AnythingService GetCommandTaskAsync: 读取{domain}队列中的任务, 第{i}次");
                }
                if (!_serverNodeQueues.TryGetValue(domain, out Queue<CommandInfoTaskDto>? queue) || queue is null)
                {
                    if (writeLog)
                    {
                        LoggerHelper.LogInformation($"AnythingService GetCommandTaskAsync: {domain}对应的队列还未创建");
                    }
                    await Task.Delay(1000, CancellationToken.None);
                    continue;
                }
                if (queue.TryDequeue(out CommandInfoTaskDto? commandTask))
                {
                    return commandTask!;
                }
                await Task.Delay(1000, CancellationToken.None);
            }
        }
        /// <summary>
        /// 设置命令执行的结果
        /// </summary>
        /// <param name="cmdExeNo"></param>
        /// <param name="commandResult"></param>
        public static void SetCommandResult(string cmdExeNo, CommandResult commandResult)
        {
            _remoteCommandResults[cmdExeNo] = commandResult;
        }
        static async Task<CommandResult> GetCommandResultAsync(string cmdExeNo)
        {
            for (int i = 0; i < 20; i++)
            {
                if (_remoteCommandResults.TryGetValue(cmdExeNo, out CommandResult? value))
                {
                    LoggerHelper.LogCritical($"AnythingService: 获取到命令[{cmdExeNo}]执行的结果:{JsonConvert.SerializeObject(value)}");
                    _remoteCommandResults.Remove(cmdExeNo);
                    return value;
                }
                await Task.Delay(1000);
                if ((i + 1) % 10 == 0)
                {
                    LoggerHelper.LogInformation($"AnythingService: 等待命令[{cmdExeNo}]返回结果..., 结果容器长度{_remoteCommandResults.Count}");
                    foreach (var item in _remoteCommandResults)
                    {
                        LoggerHelper.LogInformation($"AnythingService: {item.Key}: {item.Value.Message}");
                    }
                }
            }
            return new CommandResult(false, "执行时间过长, 请稍后查看执行结果");
        }
        string AnythingInfoCacheKey(int settingId) => $"AnythingInfo_{settingId}";
        /// <summary>
        /// 根据settingId获取对应的AnythingInfo
        /// </summary>
        /// <param name="settingId"></param>
        /// <param name="commandName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<AnythingInfo> GetAnythingInfoBySettingIdAsync(int settingId)
        {
            AnythingInfo? anythingInfo;
            if (memoryCache.TryGetValue(_cacheKeyAllAnythingInfos, out List<AnythingInfo>? anythingInfos) && anythingInfos is not null)
            {
                anythingInfo = anythingInfos.FirstOrDefault(x => x.SettingId == settingId);
                return anythingInfo ?? throw new Exception("无效的Anything");
            }
            string cacheKeyAnythingInfo = AnythingInfoCacheKey(settingId);
            if (memoryCache.TryGetValue(cacheKeyAnythingInfo, out anythingInfo))
            {
                return anythingInfo ?? throw new Exception("无效的Anything");
            }
            var anythingSetting = (await GetAnythingSettingByIdAsync(settingId)) ?? throw new Exception($"无效的AnythingSetting");
            var commands = await GetAnythingCommandsAsync(anythingSetting.Id);
            var anythingDetails = anythingSetting.ToDetails(commands);
            anythingInfo = await BuildAnythingInfoAsync(anythingDetails);

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(60 * 8));
            memoryCache.Set(cacheKeyAnythingInfo, anythingInfo, cacheEntryOptions);
            return anythingInfo;
        }
        static readonly ConcurrentDictionary<int, Func<object[], Task<CommandResult>>> AnythingIdAndCommandExecutorMap = new();
        /// <summary>
        /// 从AnythingSetting解析一个AnythingInfo对象
        /// </summary>
        /// <param name="anythingSettingDetails"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<AnythingInfo> BuildAnythingInfoAsync(AnythingSettingDetails anythingSettingDetails)
        {
            #region 解析Properties, 添加Name和Title
            var properties = GetAllProperties(anythingSettingDetails);
            #endregion

            #region 解析Executor对象和构造函数参数(使用Properties解析参数)
            AnythingExecutor? anythingExecutor;
            if (anythingSettingDetails.Executor == 0)
            {
                anythingExecutor = new AnythingExecutor { Name = "SystemCmd" };
            }
            else
            {
                // 在查询Anything列表时, 多个AnythingInfo都可能对应一个Executor, 所以此查询在一瞬间可能很频繁, 所以添加了缓存
                string cacheKey = $"CacheKeyExecutor_{anythingSettingDetails.Executor}";
                if (!memoryCache.TryGetValue(cacheKey, out anythingExecutor) || anythingExecutor is null)
                {
                    anythingExecutor = await executorRepository.GetByIdAsync(anythingSettingDetails.Executor) ?? throw new Exception($"无效的AnythingExecutor: {anythingSettingDetails.Executor}");
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
            AnythingIdAndCommandExecutorMap.AddOrUpdate(anythingSettingDetails.Id, anythingCommandExecutor, (k, v) => anythingCommandExecutor);
            #endregion

            #region 解析出AnythingInfo
            // 解析CommandTxt中的模板
            foreach (var anythingCommand in anythingSettingDetails.Commands)
            {
                anythingCommand.CommandTxt = TmplHelper.ResolveExpressionValue(anythingCommand.CommandTxt, properties)?.ToString() ?? throw new Exception($"解析命令\"{anythingCommand.CommandTxt}\"异常");
                if (!string.IsNullOrWhiteSpace(anythingCommand.ExecutedState))
                {
                    var start = DateTime.Now;
                    anythingCommand.ExecutedState = (await anythingCommandExecutor([anythingCommand.ExecutedState])).Message;
                    Console.WriteLine($"获取命令状态耗时: {(DateTime.Now - start).TotalMilliseconds}/ms");
                }
            }

            var anythingInfo = new AnythingInfo()
            {
                SettingId = anythingSettingDetails.Id,
                CommandExecutor = executorName,
                Name = anythingSettingDetails.Name,
                Title = anythingSettingDetails.Title,
                Properties = properties,
                Commands = anythingSettingDetails.Commands
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

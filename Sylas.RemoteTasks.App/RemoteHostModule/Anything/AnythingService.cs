using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Dto;
using Sylas.RemoteTasks.Utils.Template;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// 操作业务数据(自定义命令的配置集)
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="executorRepository"></param>
    /// <param name="memoryCache"></param>
    public class AnythingService(RepositoryBase<AnythingSetting> repository, RepositoryBase<AnythingExecutor> executorRepository, IMemoryCache memoryCache)
    {
        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="dataFilter"></param>
        /// <returns></returns>
        public async Task<PagedData<AnythingSetting>> GetAnythingSettingsAsync(int pageIndex, int pageSize, string orderField, bool isAsc = false, DataFilter? dataFilter = null)
        {
            if (string.IsNullOrWhiteSpace(orderField))
            {
                orderField = nameof(AnythingSetting.UpdateTime);
            }
            var snippetPage = await repository.GetPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            return snippetPage;
        }

        /// <summary>
        /// 根据Id查询
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="dataFilter"></param>
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
            return added > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<OperationResult> UpdateAnythingSettingAsync(AnythingSetting anythingSetting)
        {
            var added = await repository.UpdateAsync(anythingSetting);
            return added > 0 ? new OperationResult(true) : new OperationResult(false, "Affected rows: 0");
        }

        const string _cacheKeyAllAnythingInfos = "AllAnythingInfos";
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
            var anythingSettingsPage = await GetAnythingSettingsAsync(1, 1000, "");
            if (anythingSettingsPage.Data is not null)
            {
                foreach (var anythingSetting in anythingSettingsPage.Data)
                {
                    var anythingInfo = await BuildAnythingInfoAsync(anythingSetting);
                    anythingInfos.Add(anythingInfo);
                }
            }
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(1));
            memoryCache.Set(_cacheKeyAllAnythingInfos, anythingInfos, cacheEntryOptions);
            return anythingInfos;
        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<CommandResult> ExecuteAsync(CommandInfoInDto dto)
        {
            var anythingSetting = (await GetAnythingSettingByIdAsync(dto.SettingId)) ?? throw new Exception($"无效的AnythingSetting");
            var anythingInfo = await BuildAnythingInfoAsync(anythingSetting);
            var commandInfo = anythingInfo.Commands.FirstOrDefault(x => x.Name == dto.CommandName) ?? throw new Exception($"未知的命令{dto.CommandName}");
            if (anythingInfo.CommandExecutor is null)
            {
                throw new Exception("Anything Executor Error");
            }
            CommandResult cr = await anythingInfo.CommandExecutor.ExecuteAsync(commandInfo.CommandTxt);
            return cr;
        }
        /// <summary>
        /// 从AnythingSetting解析一个AnythingInfo对象
        /// </summary>
        /// <param name="anything"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<AnythingInfo> BuildAnythingInfoAsync(AnythingSetting anything)
        {
            #region 解析Properties
            var properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(anything.Properties) ?? [];
            properties.ResolveSelfTmplValues();
            #endregion

            #region 解析Executor对象和构造函数参数
            AnythingExecutor anythingExecutor;
            
            if (anything.Executor == 0)
            {
                anythingExecutor = new AnythingExecutor { Name = "SystemCmd()" };
            }
            else
            {
                anythingExecutor = await executorRepository.GetByIdAsync(anything.Executor) ?? throw new Exception($"无效的AnythingExecutor: {anything.Executor}");
            }
            var executorName = anythingExecutor.Name;
            var argTmpls = string.IsNullOrWhiteSpace(anythingExecutor.Arguments)
                ? []
                : JsonConvert.DeserializeObject<List<ArgumentInfo>>(anythingExecutor.Arguments) ?? throw new Exception($"无效的AnythingExecutor参数:{anythingExecutor.Arguments}");
            object[] args = [];
            args = new object[argTmpls.Count];
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
            var t = ReflectionHelper.GetTypeByClassName(executorName);
            var instance = ReflectionHelper.CreateInstance(t, args);
            var executeCommandMethod = t.GetMethod("ExecuteCommand");
            var anythingCommandExecutor = instance as ICommandExecutor ?? throw new Exception("从模板生成ICommandExecutor失败");
            #endregion

            #region 解析出AnythingInfo
            // 根据Properties解析其他模板
            anything.Name = TmplHelper.ResolveExpressionValue(anything.Name, properties)?.ToString() ?? throw new Exception($"Name {anything.Name} 解析失败");
            anything.Title = TmplHelper.ResolveExpressionValue(anything.Title, properties)?.ToString() ?? throw new Exception($"Title {anything.Title} 解析失败");

            // 解析CommandTxt中的模板
            var commands = JsonConvert.DeserializeObject<List<CommandInfo>>(anything.Commands) ?? [];
            foreach (var anythingCommand in commands)
            {
                anythingCommand.CommandTxt = TmplHelper.ResolveExpressionValue(anythingCommand.CommandTxt, properties)?.ToString() ?? throw new Exception($"解析命令\"{anythingCommand.CommandTxt}\"异常");
            }

            var anythingInfo = new AnythingInfo()
            {
                SettingId = anything.Id,
                CommandExecutor = anythingCommandExecutor,
                Name = anything.Name,
                Title = anything.Title,
                Properties = properties,
                Commands = commands
            };
            #endregion

            return anythingInfo;
        }
    }
}

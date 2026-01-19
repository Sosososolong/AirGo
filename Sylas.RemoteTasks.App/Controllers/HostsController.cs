using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sylas.RemoteTasks.App.DatabaseManager.Models;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Template;
using Sylas.RemoteTasks.Utils.Template.Dtos;
using System.Linq;
using System.Text;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class HostsController(ILoggerFactory loggerFactory, AnythingService anythingService) : CustomBaseController
    {
        public ILogger Logger { get; } = loggerFactory.CreateLogger<HostsController>();
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 命令配置的分页查询
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> AnythingSettingsAsync([FromBody] DataSearch? search = null)
        {
            var anythingSettings = await anythingService.GetAnythingSettingsAsync(search);
            var result = new RequestResult<PagedData<AnythingSetting>>(anythingSettings);
            return Ok(result);
        }
        /// <summary>
        /// 根据id查询命令配置和解析后的命令信息
        /// </summary>
        /// <returns></returns>
        public async Task<RequestResult<object>> AnythingSettingAndInfoAsync(int id)
        {
            if (id <= 0)
            {
                return RequestResult<object>.Error("id不能为空");
            }
            var anythingDetails = await anythingService.GetAnythingSettingDetailsByIdAsync(id);
            if (anythingDetails is null)
            {
                return RequestResult<object>.Error($"未找到id为{id}的操作对象");
            }
            var anythingInfo = await anythingService.GetAnythingInfoBySettingIdAsync(id);

            return RequestResult<object>.Success(new { AnythingSetting = anythingDetails, AnythingInfo = anythingInfo });
        }

        /// <summary>
        /// 显示所有命令
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult AnythingInfosAsync()
        {
            return View();
        }

        /// <summary>
        /// 查询命令执行器列表
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public async Task<IActionResult> Executors(int pageIndex, int pageSize)
        {
            var pageData = await anythingService.ExecutorsAsync(new DataSearch { PageIndex = pageIndex == 0 ? 1 : pageIndex, PageSize = pageSize == 0 ? 20 : pageSize });
            return Ok(RequestResult<PagedData<AnythingExecutor>>.Success(pageData));
        }
        //static Regex validMsgPattern = new(@"(?<=.*[A-Z]:\\.*>).+");
        /// <summary>
        /// 对指定对象anything执行指定的命令command
        /// </summary>
        /// <param name="anything"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task ExecuteCommandAsync([FromBody] CommandInfoInDto commandInfoInDto)
        {
            var response = HttpContext.Response;
            response.Headers.Append("Content-Type", "text/event-stream");
            response.Headers.Append("Cache-Control", "no-cache");
            response.Headers.Append("Connection", "keep-alive");
            var cancellationToken = HttpContext.RequestAborted;
            try
            {
                var commandResults = anythingService.ExecuteAsync(commandInfoInDto);
                await foreach (CommandResult commandResult in commandResults)
                {
                    string commandResultJosn = JsonConvert.SerializeObject(commandResult, new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                    // 有可能连续写入两次, 客户端一起接收过来了, 所以这里只返回有效数据, 由客户端进行拆分; 原本是: "data: {commandResultJosn}\n"
                    await response.WriteAsync($"{commandResultJosn}\n", Encoding.UTF8);

                    await response.Body.FlushAsync();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LoggerHelper.LogCritical("客户端取消请求");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                string commandResultJosn = JsonConvert.SerializeObject(new CommandResult(false, ex.Message), new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                await response.WriteAsync($"{commandResultJosn}\n", Encoding.UTF8);
                await response.Body.FlushAsync();
            }
            finally
            {
                string endJson = JsonConvert.SerializeObject(new CommandResult(false, string.Empty, "-cmd-end"), new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                await response.WriteAsync($"{endJson}\n", Encoding.UTF8);
                await response.Body.FlushAsync();
            }
            
            LoggerHelper.LogCritical("命令执行完毕");
        }
        /// <summary>
        /// 对指定对象anything执行指定的命令command
        /// </summary>
        /// <param name="anything"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task ExecuteCommandsAsync([FromBody] CommandInfoInDto[] commandInfoInDtos)
        {
            var response = HttpContext.Response;
            response.Headers.Append("Content-Type", "text/event-stream");
            response.Headers.Append("Cache-Control", "no-cache");
            response.Headers.Append("Connection", "keep-alive");
            var cancellationToken = HttpContext.RequestAborted;

            foreach (var commandInfoInDto in commandInfoInDtos)
            {
                var commandResults = anythingService.ExecuteAsync(commandInfoInDto);
                await foreach (CommandResult commandResult in commandResults)
                {
                    string commandResultJosn = JsonConvert.SerializeObject(commandResult, new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                    // 有可能连续写入两次, 客户端一起接收过来了, 所以这里只返回有效数据, 由客户端进行拆分; 原本是: "data: {commandResultJosn}\n"
                    await response.WriteAsync($"{commandResultJosn}\n", Encoding.UTF8);

                    await response.Body.FlushAsync();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LoggerHelper.LogCritical("客户端取消请求");
                        break;
                    }
                }
            }
            LoggerHelper.LogInformation("命令执行完毕");
        }
        /// <summary>
        /// 添加一条AnythingSetting记录
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddAnythingSettingAsync([FromBody] AnythingSetting anythingSetting)
        {
            return Json(await anythingService.AddAnythingSettingAsync(anythingSetting));
        }
        /// <summary>
        /// AnythingSetting分页查询
        /// </summary>
        /// <param name="search">分页查询参数</param>
        /// <returns></returns>
        public async Task<IActionResult> GetAnythingSettingsAsync([FromBody] DataSearch? search = null)
        {
            search ??= new();
            var anythingSettings = await anythingService.GetAnythingSettingsAsync(search);
            return Json(anythingSettings);
        }
        /// <summary>
        /// 更新AnythingSetting
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateAnythingSettingAsync([FromBody] Dictionary<string, string> anythingSetting)
        {
            var result = await anythingService.UpdateAnythingSettingAsync(anythingSetting);
            return Json(result);
        }
        /// <summary>
        /// 更新命令内容
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateCommandAsync([FromBody] Dictionary<string, string> command)
        {
            var result = await anythingService.UpdateCommandAsync(command);
            return Ok(RequestResult<OperationResult>.Success(result));
        }
        /// <summary>
        /// 删除Anything配置
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteAnythingSettingByIdAsync(int id)
        {
            return Json(await anythingService.DeleteAnythingSettingByIdAsync(id));
        }
        /// <summary>
        /// 删除命令
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteAnythingCommandByIdAsync(int id)
        {
            return Json(await anythingService.DeleteAnythingCommandByIdAsync(id));
        }
        /// <summary>
        /// 为Anything配置添加一条命令
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddCommandAsync([FromBody] AnythingCommand command)
        {
            return Json(await anythingService.AddCommandAsync(command));
        }
        /// <summary>
        /// 解析一个命令模板
        /// </summary>
        /// <param name="dto">dto.Id:命令所属AnythingSetting的Id, 需要根据它的Properties解析命令中的模板; dto.CmdTxt:要解析的命令脚本</param>
        /// <returns></returns>
        public async Task<RequestResult<string>> ResolveCommandSetttingAsync([FromBody] CommandResolveDto dto)
        {
            return await anythingService.ResolveCommandSettingAsync(dto);
        }

        /// <summary>
        /// 获取服务器基本信息和一些应用信息
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> GetServerInfo()
        {
            var info = await SystemCmd.GetServerAndAppInfoAsync();
            return Ok(RequestResult<ServerInfo>.Success(info));
        }
        /// <summary>
        /// 服务器和应用状态数据面板
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult ServerAndAppStatus()
        {
            return View();
        }
        /// <summary>
        /// 模板测试页面
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult TmplTest()
        {
            return View();
        }
        /// <summary>
        /// 解析模板字符串
        /// </summary>
        /// <param name="dto">模板文本和数据模型</param>
        /// <returns></returns>
        public RequestResult<string> ResolveTmpl([FromBody] ResolveTmplDto dto)
        {
            var datamodelDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(dto.DatamodelJson) ?? throw new Exception("模板数据模型json字符串转换为字典失败");
            var resolved = TmplHelper.ResolveExpressionValue(dto.TmplTxt, datamodelDictionary);
            var resolvedDataType = resolved.GetType().Name;
            var resolvedString = resolved is string resolvedStrVal ? resolvedStrVal : JsonConvert.SerializeObject(resolved);
            if (string.IsNullOrWhiteSpace(resolvedString))
            {
                return RequestResult<string>.Error("解析结果为空");
            }
            return new RequestResult<string>($"{resolvedDataType}: {resolvedString}");
        }

        [AllowAnonymous]
        public IActionResult Flows()
        {
            return View();
        }
        /// <summary>
        /// 工作流展示页面
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult AnythingFlows()
        {
            return View();
        }
        /// <summary>
        /// 为工作流添加一个节点
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddAnythingToFlow([FromServices] RepositoryBase<AnythingFlow> repository, [FromBody] FlowAddAnthingInDto dto)
        {
            var flow = await repository.GetByIdAsync(dto.FlowId);
            if (flow is null)
            {
                return Ok(RequestResult<bool>.Error($"未找到id为{dto.FlowId}的工作流"));
            }

            var anythingIdList = flow.AnythingIds.Split(',').ToList();
            anythingIdList.Insert(dto.AnythingId, dto.AnythingIndex.ToString());
            flow.AnythingIds = string.Join(',', anythingIdList);
            int affectedRows = await repository.UpdateAsync(flow);
            return affectedRows > 0 ? Ok(RequestResult<bool>.Success(true)) : Ok(RequestResult<bool>.Error("添加失败"));
        }
        /// <summary>
        /// 从工作流中删除一个节点
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="flowId"></param>
        /// <param name="removeIndex"></param>
        /// <returns></returns>
        public async Task<IActionResult> RemoveAnythingFromFlow([FromServices] RepositoryBase<AnythingFlow> repository, int flowId, int removeIndex)
        {
            var flow = await repository.GetByIdAsync(flowId);
            if (flow is null)
            {
                return Ok(RequestResult<bool>.Error($"未找到id为{flowId}的工作流"));
            }

            var anythingIdList = flow.AnythingIds.Split(',').ToList();
            anythingIdList.RemoveAt(removeIndex);
            flow.AnythingIds = string.Join(',', anythingIdList);
            int affectedRows = await repository.UpdateAsync(flow);
            return affectedRows > 0 ? Ok(RequestResult<bool>.Success(true)) : Ok(RequestResult<bool>.Error("删除失败"));
        }
        public async Task<IActionResult> ReorderFlowAnything([FromServices] RepositoryBase<AnythingFlow> repository, int flowId, int anythingIndex, bool forward)
        {
            var flow = await repository.GetByIdAsync(flowId);
            if (flow is null)
            {
                return Ok(RequestResult<bool>.Error($"未找到id为{flowId}的工作流"));
            }

            var anythingIdList = flow.AnythingIds.Split(',').ToList();
            string target = anythingIdList[anythingIndex];
            int nextIndex;
            if (forward)
            {
                nextIndex = anythingIndex + 1;
                if (nextIndex >= anythingIdList.Count)
                {
                    nextIndex = 0;
                }
            }
            else
            {
                nextIndex = anythingIndex - 1;
                if (nextIndex < 0)
                {
                    nextIndex = anythingIdList.Count - 1;
                }
            }
            anythingIdList.RemoveAt(anythingIndex);
            anythingIdList.Insert(nextIndex, target);
            flow.AnythingIds = string.Join(',', anythingIdList);
            int affectedRows = await repository.UpdateAsync(flow);
            return affectedRows > 0 ? Ok(RequestResult<bool>.Success(true)) : Ok(RequestResult<bool>.Error("排序失败"));
        }
        /// <summary>
        /// 工作流分页查询
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        public async Task<IActionResult> QueryAnythingFlowsAsync([FromServices] RepositoryBase<AnythingFlow> repository, [FromBody] DataSearch? search = null)
        {
            var page = await repository.GetPageAsync(search);
            var result = RequestResult<PagedData<AnythingFlow>>.Success(page);
            return Ok(result);
        }
        /// <summary>
        /// 添加工作流
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddAnythingFlowAsync([FromServices] RepositoryBase<AnythingFlow> repository, [FromBody] AnythingFlow entity)
        {
            int affectedRows = await repository.AddAsync(entity);
            return Ok(affectedRows > 0 ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error("添加失败"));
        }
        /// <summary>
        /// 添加工作流
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="flow"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateAnythingFlowAsync([FromServices] RepositoryBase<AnythingFlow> repository, [FromBody] AnythingFlow flow)
        {
            int affectedRows = await repository.UpdateAsync(flow);
            return Ok(affectedRows > 0 ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error("更新备份数据失败"));
        }
        /// <summary>
        /// 删除工作流
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteAnythingFlowAsync([FromServices] RepositoryBase<AnythingFlow> repository, [FromBody] int id)
        {
            var entity = await repository.GetByIdAsync(id);
            if (entity is null)
            {
                return Ok(RequestResult<bool>.Error("未找到备份信息"));
            }
            int affectedRows = await repository.DeleteAsync(id);

            return Ok(affectedRows > 0 ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error("删除失败"));
        }
        /// <summary>
        /// 将命令的环境变量同步到工作流
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> SyncEnvVarsAsync([FromServices] RepositoryBase<AnythingFlow> repository, [FromBody] int id)
        {
            var flow = await repository.GetByIdAsync(id);
            if (flow is null)
            {
                return Ok(RequestResult<bool>.Error($"工作流{id}不存在"));
            }
            Dictionary<string, object> flowVars = string.IsNullOrWhiteSpace(flow.EnvVars) ? [] : JsonConvert.DeserializeObject<Dictionary<string, object>>(flow.EnvVars) ?? [];
            if (flow is null)
            {
                return Ok(RequestResult<bool>.Error($"未找到id为{id}的工作流"));
            }

            var anythingIdList = flow.AnythingIds.Split(',').ToList();

            foreach (var anythingId in anythingIdList)
            {
                var anythingIdInt = Convert.ToInt32(anythingId);
                // 不要获取AnythingInfo, 因为它包含的变量包含了Name和Title; 使用AnythingSetting这才是手动设置的变量
                var anythingSetting = await anythingService.GetAnythingSettingByIdAsync(anythingIdInt);
                if (anythingSetting is null)
                {
                    return Ok(RequestResult<bool>.Error($"未找到id为{anythingIdInt}的节点"));
                }
                Dictionary<string, object> anythingProps = string.IsNullOrWhiteSpace(anythingSetting.Properties)
                    ? []
                    : JsonConvert.DeserializeObject<Dictionary<string, object>>(anythingSetting.Properties) ?? [];
                foreach (var k in anythingProps.Keys)
                {
                    if (!flowVars.Any(x => x.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        flowVars[k] = anythingProps[k];
                    }
                }
            }

            flow.EnvVars = JsonConvert.SerializeObject(flowVars);
            int updated = await repository.UpdateAsync(flow);
            
            return Ok(RequestResult<bool>.Success(updated > 0));
        }
    }
}

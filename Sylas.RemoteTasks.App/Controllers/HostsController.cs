using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Template;
using Sylas.RemoteTasks.Utils.Template.Dtos;
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
            LoggerHelper.LogCritical("命令执行完毕");
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
    }
}

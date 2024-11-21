using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Dto;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class HostsController(ILoggerFactory loggerFactory, HostService hostService, AnythingService anythingService) : CustomBaseController
    {
        private readonly HostService _hostService = hostService;

        public ILogger Logger { get; } = loggerFactory.CreateLogger<HostsController>();

        public IActionResult Index()
        {
            var hostProviders = _hostService.GetRemoteHosts();
            return View(hostProviders);
        }

        /// <summary>
        /// 执行 远程主机信息(Docker容器等) 动态生成的对应命令
        /// </summary>
        /// <param name="executeDto"></param>
        /// <returns></returns>
        public IActionResult Execute([FromBody] ExecuteDto executeDto)
        {
            return Json(_hostService.Execute(executeDto));
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
            var anythingSetting = await anythingService.GetAnythingSettingByIdAsync(id);
            if (anythingSetting is null)
            {
                return RequestResult<object>.Error($"未找到id为{id}的操作对象");
            }
            var anythingInfo = await anythingService.GetAnythingInfoBySettingAndCommandAsync(id);

            return RequestResult<object>.Success(new { AnythingSetting = anythingSetting, AnythingInfo = anythingInfo });
        }

        /// <summary>
        /// 显示所有命令
        /// </summary>
        /// <returns></returns>
        public IActionResult AnythingInfosAsync()
        {
            return View();
        }

        /// <summary>
        /// 对指定对象anything执行指定的命令command
        /// </summary>
        /// <param name="anything"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<IActionResult> ExecuteCommandAsync([FromBody] CommandInfoInDto commandInfoInDto)
        {
            var commandResult = await anythingService.ExecuteAsync(commandInfoInDto);
            return Ok(RequestResult<CommandResult>.Success(commandResult));
        }
        /// <summary>
        /// 添加一条AnythingSetting记录
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddAnythingSettingAsync(AnythingSetting anythingSetting)
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
        public async Task<IActionResult> DeleteAnythingSettingByIdAsync(int id)
        {
            return Json(await anythingService.DeleteAnythingSettingByIdAsync(id));
        }
        /// <summary>
        /// 解析一个命令模板
        /// </summary>
        /// <param name="id">命令所属AnythingSetting的Id, 需要根据它的Properties解析命令中的模板</param>
        /// <param name="command">带模板字符串的命令</param>
        /// <returns></returns>
        public async Task<RequestResult<string>> ResolveCommandSetttingAsync(int id, string command)
        {
            return await anythingService.ResolveCommandSettingAsync(id, command);
        }

        /// <summary>
        /// 服务器和应用状态数据面板
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> ServerAndAppStatus()
        {
            var info = await SystemCmd.GetServerAndAppInfoAsync();
            return View(info);
        }
    }
}

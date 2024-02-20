using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Utils.CommandExecutor;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class HostsController(ILoggerFactory loggerFactory, HostService hostService) : Controller
    {
        private readonly HostService _hostService = hostService;

        public ILogger Logger { get; } = loggerFactory.CreateLogger<HostsController>();

        public IActionResult Index()
        {
            var _hostsManager = _hostService.GetHostsManagers();
            return View(_hostsManager);
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
        /// 显示所有命令
        /// </summary>
        /// <returns></returns>
        public IActionResult AnythingInfos()
        {
            return View(AnythingInfo.AnythingInfos);
        }
        /// <summary>
        /// 对指定对象anything执行指定的命令command
        /// </summary>
        /// <param name="anything"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<IActionResult> ExecuteCommandAsync([FromBody] CommandInfoInDto commandInfoInDto)
        {
            var commandResult = await AnythingInfo.ExecuteAsync(commandInfoInDto);
            return Ok(commandResult);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.RemoteHostModule;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class HostsController : Controller
    {
        private readonly HostService _hostService;

        public HostsController(ILoggerFactory loggerFactory, HostService hostService)
        {
            _logger = loggerFactory.CreateLogger<HostsController>();
            _hostService = hostService;
        }

        public ILogger _logger { get; }

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
    }
}

﻿using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class HostsController(ILoggerFactory loggerFactory, HostService hostService, AnythingService anythingService) : Controller
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
        /// 显示所有命令
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> AnythingInfosAsync()
        {
            var anythingInfos = await anythingService.GetAllAnythingInfosAsync();
            return View(anythingInfos);
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
            return Ok(commandResult);
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
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="dataFilter"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetAnythingSettingsAsync(int pageIndex, int pageSize, string orderField, bool isAsc = true, [FromBody] DataFilter? dataFilter = null)
        {
            var anythingSettings = await anythingService.GetAnythingSettingsAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            return Json(anythingSettings);
        }
        /// <summary>
        /// 更新AnythingSetting
        /// </summary>
        /// <param name="anythingSetting"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateAnythingSettingAsync([FromBody] AnythingSetting anythingSetting)
        {
            var result = await anythingService.UpdateAnythingSettingAsync(anythingSetting);
            return Json(result);
        }
        public async Task<IActionResult> DeleteAnythingSettingByIdAsync(int id)
        {
            return Json(await anythingService.DeleteAnythingSettingByIdAsync(id));
        }
        /// <summary>
        /// 服务器和应用状态数据
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> ServerAndAppStatus()
        {
            var info = await SystemCmd.GetServerAndAppInfoAsync();
            return View(info);
        }
    }
}

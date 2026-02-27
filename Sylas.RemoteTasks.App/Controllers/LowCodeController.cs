using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.LowCode;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.Controllers
{
    /// <summary>
    /// 低代码VDS页面管理控制器
    /// </summary>
    public class LowCodeController(RepositoryBase<VdsPage> repository) : CustomBaseController
    {
        #region VDS配置管理页面
        /// <summary>
        /// VDS配置管理首页
        /// </summary>
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }
        #endregion

        #region VDS配置CRUD接口
        /// <summary>
        /// 分页查询VDS页面配置
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Pages([FromBody] DataSearch? search = null)
        {
            search ??= new();
            var pages = await repository.GetPageAsync(search);
            return Json(new RequestResult<PagedData<VdsPage>>(pages));
        }

        /// <summary>
        /// 根据ID获取单个VDS配置
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetPage([FromBody] int id)
        {
            var page = await repository.GetByIdAsync(id);
            if (page is null)
            {
                return Ok(RequestResult<VdsPage>.Error("配置不存在"));
            }
            return Json(new RequestResult<VdsPage>(page));
        }

        /// <summary>
        /// 添加VDS页面配置
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddPage([FromForm] VdsPage vdsPage)
        {
            // 检查Name是否已存在
            var existingPages = await repository.GetPageAsync(new DataSearch(1, 1, 
                new DataFilter { FilterItems = [new FilterItem("name", "=", vdsPage.Name)] }));
            if (existingPages.Data.Any())
            {
                return Ok(new OperationResult(false, $"页面标识 '{vdsPage.Name}' 已存在"));
            }

            var added = await repository.AddAsync(vdsPage);
            return added > 0 
                ? Ok(new OperationResult(true, "添加成功")) 
                : Ok(new OperationResult(false, "添加失败"));
        }

        /// <summary>
        /// 更新VDS页面配置
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdatePage([FromForm] VdsPage vdsPage)
        {
            var record = await repository.GetByIdAsync(vdsPage.Id);
            if (record is null)
            {
                return Ok(RequestResult<bool>.Error("配置不存在"));
            }

            // 如果Name发生变化，检查新Name是否已存在
            if (record.Name != vdsPage.Name)
            {
                var existingPages = await repository.GetPageAsync(new DataSearch(1, 1,
                    new DataFilter { FilterItems = [new FilterItem("name", "=", vdsPage.Name)] }));
                if (existingPages.Data.Any())
                {
                    return Ok(new OperationResult(false, $"页面标识 '{vdsPage.Name}' 已存在"));
                }
            }

            var updated = await repository.UpdateAsync(vdsPage);
            return updated > 0 
                ? Ok(new OperationResult(true, "更新成功")) 
                : Ok(new OperationResult(false, "更新失败"));
        }

        /// <summary>
        /// 删除VDS页面配置
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeletePage([FromBody] int id)
        {
            var page = await repository.GetByIdAsync(id);
            if (page is null)
            {
                return Ok(new OperationResult(false, "配置不存在"));
            }
            var deleted = await repository.DeleteAsync(id);
            return deleted > 0 
                ? Ok(new OperationResult(true, "删除成功")) 
                : Ok(new OperationResult(false, "删除失败"));
        }
        #endregion

        #region 动态页面渲染
        /// <summary>
        /// 根据页面名称动态渲染VDS页面
        /// </summary>
        /// <param name="pageName">页面标识名称</param>
        [AllowAnonymous]
        [HttpGet("LowCode/Render/{pageName}")]
        public async Task<IActionResult> Render(string pageName)
        {
            // 根据pageName查询VDS配置
            var pages = await repository.GetPageAsync(new DataSearch(1, 1,
                new DataFilter { FilterItems = [new FilterItem("name", "=", pageName)] }));
            
            var vdsPage = pages.Data.FirstOrDefault();
            if (vdsPage is null)
            {
                return NotFound($"页面 '{pageName}' 不存在");
            }

            if (!vdsPage.IsEnabled)
            {
                return NotFound($"页面 '{pageName}' 已禁用");
            }

            return View("Render", vdsPage);
        }

        /// <summary>
        /// 获取所有已启用的VDS页面列表（用于导航菜单）
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetEnabledPages()
        {
            var pages = await repository.GetPageAsync(new DataSearch(1, 100,
                new DataFilter { FilterItems = [new FilterItem("isEnabled", "=", true)] },
                [new OrderField("orderNo", true)]));
            
            var result = pages.Data.Select(p => new { p.Name, p.Title, p.Description });
            return Json(new RequestResult<object>(result));
        }
        #endregion
    }
}

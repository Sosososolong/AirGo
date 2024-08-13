using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.DatabaseManager;
using Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Constants;
using Sylas.RemoteTasks.Utils.Dto;

namespace Sylas.RemoteTasks.App.Controllers
{
    /// <summary>
    /// 数据库管理
    /// </summary>
    /// <param name="repository"></param>
    public class DatabaseController(DbConnectionInfoRepository repository) : CustomBaseController
    {
        public IActionResult Index()
        {
            return View();
        }
        /// <summary>
        /// 数据库连接字符串信息分页查询
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="dataFilter"></param>
        /// <returns></returns>
        public async Task<IActionResult> ConnectionStringsAsync(int pageIndex, int pageSize, string orderField, bool isAsc = true, [FromBody] DataFilter? dataFilter = null)
        {
            var page = await repository.GetPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter ?? new DataFilter());
            return Ok(page);
        }
        /// <summary>
        /// 添加一个数据库连接字符串信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddConnectionStringAsync([FromBody] DbConnectionInfoInDto dto)
        {
            int affectedRows = await repository.AddAsync(dto);
            return Ok(new OperationResult(affectedRows > 0));
        }
        /// <summary>
        /// 更新数据库连接信息
        /// </summary>
        /// <param name="connInfo"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateConnectionStringAsync([FromBody] DbConnectionInfoUpdateDto dto)
        {
            var connInfo = await repository.GetByIdAsync(dto.Id);
            if (connInfo is null)
            {
                return Ok(new OperationResult(false, "数据不存在"));
            }
            connInfo.Name = dto.Name;
            connInfo.Alias = dto.Alias;
            connInfo.ConnectionString = dto.ConnectionString;
            connInfo.Remark = dto.Remark;
            connInfo.OrderNo = dto.OrderNo;
            connInfo.UpdateTime = DateTime.Now;
            int affectedRows = await repository.UpdateAsync(connInfo);
            return Ok(new OperationResult(affectedRows > 0));
        }
        /// <summary>
        /// 克隆一个数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> CloneConnectionStringAsync([FromBody] int id)
        {
            var affectedRows = await repository.CloneAsync(id);
            return Ok(new OperationResult(affectedRows > 0));
        }
        /// <summary>
        /// 删除数据库连接字符串信息
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteConnectionStringsAsync([FromBody] string ids)
        {
            int affectedRows = 0;
            foreach (var id in ids.Split(','))
            {
                affectedRows += await repository.DeleteAsync(Convert.ToInt32(id));
            }
            return Ok(new OperationResult(affectedRows > 0));
        }
    }
}

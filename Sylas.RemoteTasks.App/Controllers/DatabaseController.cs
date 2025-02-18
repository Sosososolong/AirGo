using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.DatabaseManager.Models;
using Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.Controllers
{
    /// <summary>
    /// 数据库管理
    /// </summary>
    /// <param name="repository"></param>
    public class DatabaseController(RepositoryBase<DbConnectionInfo> repository) : CustomBaseController
    {
        public IActionResult Index()
        {
            return View();
        }
        /// <summary>
        /// 数据库连接字符串信息分页查询
        /// </summary>
        /// <param name="search">分页查询参数</param>
        /// <returns></returns>
        public async Task<IActionResult> ConnectionStringsAsync([FromBody] DataSearch? search = null)
        {
            search ??= new();
            var page = await repository.GetPageAsync(search);
            var result = new RequestResult<PagedData<DbConnectionInfo>>(page);
            return Ok(result);
        }
        /// <summary>
        /// 添加一个数据库连接字符串信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddConnectionStringAsync([FromBody] DbConnectionInfoInDto dto)
        {
            int affectedRows = await repository.AddAsync(dto.ToEntity());
            return Ok(new RequestResult<bool>(affectedRows > 0));
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
            return Ok(new RequestResult<bool>(affectedRows > 0));
        }
        /// <summary>
        /// 克隆一个数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> CloneConnectionStringAsync([FromBody] int id)
        {
            var connInfo = await repository.GetByIdAsync(id);
            if (connInfo is null)
            {
                return Ok(new RequestResult<bool>(false) { ErrMsg = "记录不存在" });
            }
            var affectedRows = await repository.AddAsync(connInfo);
            return Ok(new RequestResult<bool>(affectedRows > 0));
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
            return Ok(new RequestResult<bool>(affectedRows > 0));
        }
    }
}

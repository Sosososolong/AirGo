using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.DatabaseManager.Models;
using Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Constants;

namespace Sylas.RemoteTasks.App.Controllers
{
    /// <summary>
    /// 数据库管理
    /// </summary>
    /// <param name="repository"></param>
    public class DatabaseController(RepositoryBase<DbConnectionInfo> repository, ILogger<DatabaseController> logger) : CustomBaseController
    {
        [AllowAnonymous]
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
            if (search.Rules is null || search.Rules.Count == 0)
            {
                search.Rules =
                [
                    new(nameof(DbConnectionInfo.OrderNo), true)
                ];
            }
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
            dto.ConnectionString = await SecurityHelper.AesEncryptAsync(dto.ConnectionString);
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
            if (DatabaseConstants.ConnectionStringKeywords.Any(x => dto.ConnectionString.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                dto.ConnectionString = await SecurityHelper.AesEncryptAsync(dto.ConnectionString);
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
            return Ok(RequestResult<bool>.Success(affectedRows > 0));
        }
        /// <summary>
        /// 备份数据库到文件中
        /// </summary>
        /// <param name="backups"></param>
        /// <param name="backupDto"></param>
        /// <returns></returns>
        public async Task<IActionResult> BackupAsync([FromServices] RepositoryBase<DbBackup> backups, [FromBody] BackupInDto backupDto)
        {
            var connInfo = await repository.GetByIdAsync(backupDto.DbConnectionInfoId);
            if (connInfo is null)
            {
                return Ok(new RequestResult<bool>(false) { ErrMsg = "记录不存在" });
            }
            string connectionString = await SecurityHelper.AesDecryptAsync(connInfo.ConnectionString);
            string backupDir = await DatabaseInfo.BackupDataAsync(connectionString, backupDto.Tables, connInfo.Name.Replace(":", string.Empty));
            int affectedRows = await backups.AddAsync(new DbBackup(backupDto.DbConnectionInfoId, AppStatus.Domain, backupDto.Name, backupDir, backupDto.Remark));
            var result = RequestResult<bool>.Success(true);
            if (affectedRows == 0)
            {
                logger.LogError("备份信息保存失败");
                result.ErrMsg = "备份信息保存失败";
            }
            return Ok(result);
        }
        /// <summary>
        /// 备份记录页面
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult Backups()
        {
            return View();
        }
        /// <summary>
        /// 历史备份记录分页查询
        /// </summary>
        /// <param name="backups"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        public async Task<IActionResult> BackupRecordsAsync([FromServices] RepositoryBase<DbBackup> backups, [FromBody] DataSearch? search = null)
        {
            var page = await backups.GetPageAsync(search);
            var result = RequestResult<PagedData<DbBackup>>.Success(page);
            return Ok(result);
        }
        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="backups"></param>
        /// <param name="backup"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddBackupAsync([FromServices] RepositoryBase<DbBackup> backups, [FromBody] DbBackup backup)
        {
            int affectedRows = await backups.AddAsync(backup);
            return Ok(affectedRows > 0 ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error("添加备份数据失败"));
        }
        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="backups"></param>
        /// <param name="backup"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateBackupAsync([FromServices] RepositoryBase<DbBackup> backups, [FromBody] DbBackup backup)
        {
            int affectedRows = await backups.UpdateAsync(backup);
            return Ok(affectedRows > 0 ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error("更新备份数据失败"));
        }
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="backups"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteBackupAsync([FromServices] RepositoryBase<DbBackup> backups, [FromBody] int id)
        {
            DbBackup? backup = await backups.GetByIdAsync(id);
            if (backup is null)
            {
                return Ok(RequestResult<bool>.Error("未找到备份信息"));
            }
            // 删除备份记录
            int affectedRows = await backups.DeleteAsync(id);

            // 删除备份目录
            Directory.Delete(backup.BackupDir, true);

            return Ok(affectedRows > 0 ? RequestResult<bool>.Success(true) : RequestResult<bool>.Error("删除备份数据失败"));
        }
        /// <summary>
        /// 还原数据库
        /// </summary>
        /// <param name="backups"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<IActionResult> RestoreAsync([FromServices] RepositoryBase<DbBackup> backups, [FromServices] IConfiguration configuration, [FromBody] RestoreInDto dto)
        {
            var backup = await backups.GetByIdAsync(dto.Id);
            if (backup is null)
            {
                return Ok(RequestResult<bool>.Error($"备份数据{dto.Id}不存在"));
            }

            DbConnectionInfo conn = await repository.GetByIdAsync(dto.RestoreConnectionId) ?? throw new Exception($"要还原的数据库连接{dto.RestoreConnectionId}不存在");
            string connStr = await SecurityHelper.AesDecryptAsync(conn.ConnectionString);

            var allowedConnectionStringKeywords = configuration.GetSection("AllowedConnectionStringKeywords").Get<string[]>();
            if (allowedConnectionStringKeywords is null || !allowedConnectionStringKeywords.Any(x => connStr.Contains($"{x}")))
            {
                return Ok(RequestResult<bool>.Error($"不允许还原到数据库\"{conn.Name}\""));
            }

            await DatabaseInfo.RestoreTablesAsync(connStr, backup.BackupDir, dto.Tables);
            return Ok(RequestResult<bool>.Success(true));
        }
    }
}

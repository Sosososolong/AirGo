﻿using Sylas.RemoteTasks.App.DatabaseManager.Models;
using Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Extensions;
using Sylas.RemoteTasks.Utils;
using System.Text;

namespace Sylas.RemoteTasks.App.DatabaseManager
{
    public class DbConnectionInfoRepository(IDatabaseProvider databaseProvider)
    {
        private readonly IDatabaseProvider _db = databaseProvider;

        #region DbConnectionString
        /// <summary>
        /// 分页查询多个数据库连接字符串信息
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<PagedData<DbConnectionInfo>> GetPageAsync(int pageIndex, int pageSize, string orderField, bool isAsc, DataFilter filter)
        {
            var pages = await _db.QueryPagedDataAsync<DbConnectionInfo>(DbConnectionInfo.TableName, pageIndex, pageSize, orderField, isAsc, filter);
            return pages;
        }
        /// <summary>
        /// 根据Id查询数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<DbConnectionInfo?> GetByIdAsync(int id)
        {
            var pages = await _db.QueryPagedDataAsync<DbConnectionInfo>(DbConnectionInfo.TableName, 1, 1, "id", true, new DataFilter { FilterItems = [new("id", "=", id)] });
            var connectionString = pages.Data.FirstOrDefault();
            if (connectionString is null)
            {
                return null;
            }
            return connectionString;
        }
        /// <summary>
        /// 添加一个新的数据库连接字符串信息
        /// </summary>
        /// <param name="dbConnectionString"></param>
        /// <returns></returns>
        public async Task<int> AddAsync(DbConnectionInfoInDto dbConnectionString)
        {
            if (!dbConnectionString.ConnectionString.IsDbConnectionString())
            {
                throw new Exception("数据库连接字符串格式异常");
            }
            if (dbConnectionString.OrderNo == 0)
            {
                dbConnectionString.OrderNo = 100;
            }
            dbConnectionString.ConnectionString = SecurityHelper.AesEncrypt(dbConnectionString.ConnectionString);
            string sql = $"insert into {DbConnectionInfo.TableName} (Name, Alias, ConnectionString, Remark, OrderNo, CreateTime, UpdateTime) values(@name, @alias, @connectionString, @remark, @orderNo, @createTime, @updateTime)";
            var parameters = new Dictionary<string, object>
            {
                { "name", dbConnectionString.Name },
                { "alias", dbConnectionString.Alias },
                { "connectionString", dbConnectionString.ConnectionString },
                { "remark", dbConnectionString.Remark },
                { "orderNo", dbConnectionString.OrderNo },
                { "createTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "updateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };
            return await _db.ExecuteSqlAsync(sql, parameters);
        }
        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="dbInfo"></param>
        /// <returns></returns>
        public async Task<int> CloneAsync(int id)
        {
            var dbInfo = await GetByIdAsync(id) ?? throw new Exception("数据库不存在");

            dbInfo.Name = dbInfo.Name.GetCopiedName();

            var dto = new DbConnectionInfoInDto { Name = dbInfo.Name, Alias = dbInfo.Alias, ConnectionString = dbInfo.ConnectionString, OrderNo = dbInfo.OrderNo, Remark = dbInfo.Remark };
            if (!dto.ConnectionString.IsDbConnectionString())
            {
                dto.ConnectionString = SecurityHelper.AesDecrypt(dto.ConnectionString);
            }
            var affectedRows = await AddAsync(dto);
            return affectedRows;
        }
        /// <summary>
        /// 更新数据库连接字符串信息
        /// </summary>
        /// <param name="dbConnectionString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> UpdateAsync(DbConnectionInfo dbConnectionString)
        {
            if (!dbConnectionString.ConnectionString.IsDbConnectionString())
            {
                throw new Exception("数据库连接字符串格式异常");
            }
            dbConnectionString.ConnectionString = SecurityHelper.AesEncrypt(dbConnectionString.ConnectionString);
            var parameters = new Dictionary<string, object> { { "id", dbConnectionString.Id } };

            var recordCount = await _db.ExecuteSqlAsync($"select count(*) from {DbConnectionInfo.TableName} where id=@id", parameters);
            if (recordCount == 0)
            {
                throw new Exception("DbConnectionString不存在");
            }

            StringBuilder setStatement = new();
            if (!string.IsNullOrWhiteSpace(dbConnectionString.Name))
            {
                setStatement.Append($"name=@name,");
                parameters.Add("name", dbConnectionString.Name);
            }
            if (!string.IsNullOrWhiteSpace(dbConnectionString.Alias))
            {
                setStatement.Append($"alias=@alias,");
                parameters.Add("alias", dbConnectionString.Alias);
            }
            if (!string.IsNullOrWhiteSpace(dbConnectionString.ConnectionString))
            {
                setStatement.Append($"connectionString=@connectionString,");
                parameters.Add("connectionString", dbConnectionString.ConnectionString);
            }
            if (!string.IsNullOrWhiteSpace(dbConnectionString.Remark))
            {
                setStatement.Append($"remark=@remark,");
                parameters.Add("remark", dbConnectionString.Remark);
            }
            if (dbConnectionString.OrderNo != 0)
            {
                setStatement.Append($"orderNo=@orderNo,");
                parameters.Add("orderNo", dbConnectionString.OrderNo);
            }
            setStatement.Append($"updateTime=@updateTime,");
            parameters.Add("updateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            string sql = $"update {DbConnectionInfo.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 删除数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteAsync(int id)
        {
            string sql = $"delete from {DbConnectionInfo.TableName} where id=@id";
            return await _db.ExecuteSqlAsync(sql, new Dictionary<string, object> { { "id", id } });
        }
        #endregion
    }
}

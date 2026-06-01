using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.ApiTester.Repositories
{
    /// <summary>
    /// ApiTester 模块仓储, 5 张表的 CRUD 由 RepositoryBase 提供, 这里集中持有
    /// </summary>
    public class ApiTesterRepository(
        IDatabaseProvider db,
        RepositoryBase<ApiCollection> collections,
        RepositoryBase<ApiEndpoint> endpoints,
        RepositoryBase<ApiEnvironment> environments,
        RepositoryBase<ApiVariable> variables,
        RepositoryBase<ApiHistory> histories)
    {
        private readonly IDatabaseProvider _db = db;
        public RepositoryBase<ApiCollection> Collections { get; } = collections;
        public RepositoryBase<ApiEndpoint> Endpoints { get; } = endpoints;
        public RepositoryBase<ApiEnvironment> Environments { get; } = environments;
        public RepositoryBase<ApiVariable> Variables { get; } = variables;
        public RepositoryBase<ApiHistory> Histories { get; } = histories;

        /// <summary>
        /// 按集合 Id 查询接口列表(不分页, 按 OrderNo)
        /// </summary>
        public async Task<PagedData<ApiEndpoint>> GetEndpointsByCollectionAsync(int collectionId)
        {
            var search = new DataSearch(1, int.MaxValue,
                new DataFilter { FilterItems = [new FilterItem("collectionId", "=", collectionId)] },
                [new("orderNo", true), new("id", true)]);
            return await Endpoints.GetPageAsync(search);
        }

        /// <summary>
        /// 按环境 Id 查询变量列表
        /// </summary>
        public async Task<PagedData<ApiVariable>> GetVariablesByEnvAsync(int environmentId)
        {
            var search = new DataSearch(1, int.MaxValue,
                new DataFilter { FilterItems = [new FilterItem("environmentId", "=", environmentId)] },
                [new("name", true)]);
            return await Variables.GetPageAsync(search);
        }

        /// <summary>
        /// 删除集合的所有接口(用于重新导入或删除集合)
        /// </summary>
        public async Task<int> DeleteEndpointsByCollectionAsync(int collectionId)
        {
            return await _db.ExecuteSqlAsync(
                $"delete from {ApiEndpoint.TableName} where collectionId=@collectionId",
                new Dictionary<string, object> { { "collectionId", collectionId } });
        }

        /// <summary>
        /// 删除环境的所有变量
        /// </summary>
        public async Task<int> DeleteVariablesByEnvAsync(int environmentId)
        {
            return await _db.ExecuteSqlAsync(
                $"delete from {ApiVariable.TableName} where environmentId=@environmentId",
                new Dictionary<string, object> { { "environmentId", environmentId } });
        }

        /// <summary>
        /// 切换激活环境(原激活环境置非激活, 新环境置激活)
        /// </summary>
        public async Task<int> SetActiveEnvironmentAsync(int environmentId)
        {
            await _db.ExecuteSqlAsync($"update {ApiEnvironment.TableName} set isActive=0 where isActive=1", new { });
            return await _db.ExecuteSqlAsync(
                $"update {ApiEnvironment.TableName} set isActive=1 where id=@id",
                new Dictionary<string, object> { { "id", environmentId } });
        }
    }
}

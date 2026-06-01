using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    /// <summary>
    /// ApiTester 模块初始化: 启动时建表, 并确保至少存在一条 default 环境
    /// </summary>
    public class ApiTesterInitService(IServiceScopeFactory scopeFactory, ILogger<ApiTesterInitService> logger) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();

                await db.CreateTableIfNotExistAsync(ApiCollection.TableName, BuildCollectionColumns());
                await db.CreateTableIfNotExistAsync(ApiEndpoint.TableName, BuildEndpointColumns());
                await db.CreateTableIfNotExistAsync(ApiEnvironment.TableName, BuildEnvironmentColumns());
                await db.CreateTableIfNotExistAsync(ApiVariable.TableName, BuildVariableColumns());
                await db.CreateTableIfNotExistAsync(ApiHistory.TableName, BuildHistoryColumns());

                // 确保存在 default 环境且激活
                var envPage = await db.QueryPagedDataAsync<ApiEnvironment>(ApiEnvironment.TableName,
                    new DataSearch(1, 1, null, [new("id", true)]));
                if (!envPage.Data.Any())
                {
                    await db.InsertDataAsync(ApiEnvironment.TableName, [new Dictionary<string, object?>
                    {
                        { "name", "default" },
                        { "isActive", true },
                        { "createTime", DateTime.Now },
                        { "updateTime", DateTime.Now },
                    }]);
                    logger.LogInformation("ApiTester 已创建 default 环境");
                }

                logger.LogInformation("ApiTester 模块初始化完成");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ApiTester 模块初始化失败");
            }

            await base.StartAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning("Api Tester 初始化服务即将结束");
            return base.StopAsync(cancellationToken);
        }

        static List<ColumnInfo> BuildCollectionColumns() =>
        [
            new() { ColumnCode = "Id", IsPK = 1, ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "Name", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "200", IsNullable = 0 },
            new() { ColumnCode = "BaseUrl", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "500", IsNullable = 1 },
            new() { ColumnCode = "Description", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "1000", IsNullable = 1 },
            new() { ColumnCode = "EndpointCount", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "SourceType", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "20", IsNullable = 0 },
            new() { ColumnCode = "SourceContent", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "GlobalAuth", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "GlobalHeaders", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "GlobalValidators", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "CreateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
            new() { ColumnCode = "UpdateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
        ];

        static List<ColumnInfo> BuildEndpointColumns() =>
        [
            new() { ColumnCode = "Id", IsPK = 1, ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "CollectionId", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "Tag", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "100", IsNullable = 1 },
            new() { ColumnCode = "Name", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "300", IsNullable = 1 },
            new() { ColumnCode = "Method", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "10", IsNullable = 0 },
            new() { ColumnCode = "Path", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "500", IsNullable = 0 },
            new() { ColumnCode = "Params", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "Headers", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "Body", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "BodyType", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "30", IsNullable = 0 },
            new() { ColumnCode = "Auth", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "Extractors", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "Validators", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "OverrideGlobalValidators", ColumnCSharpType = "bool", ColumnType = "bit", IsNullable = 0 },
            new() { ColumnCode = "OrderNo", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "CreateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
            new() { ColumnCode = "UpdateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
        ];

        static List<ColumnInfo> BuildEnvironmentColumns() =>
        [
            new() { ColumnCode = "Id", IsPK = 1, ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "Name", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "100", IsNullable = 0 },
            new() { ColumnCode = "IsActive", ColumnCSharpType = "bool", ColumnType = "bit", IsNullable = 0 },
            new() { ColumnCode = "CreateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
            new() { ColumnCode = "UpdateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
        ];

        static List<ColumnInfo> BuildVariableColumns() =>
        [
            new() { ColumnCode = "Id", IsPK = 1, ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "EnvironmentId", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "Name", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "100", IsNullable = 0 },
            new() { ColumnCode = "Value", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "Description", ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "500", IsNullable = 1 },
            new() { ColumnCode = "IsSecret", ColumnCSharpType = "bool", ColumnType = "bit", IsNullable = 0 },
            new() { ColumnCode = "CreateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
            new() { ColumnCode = "UpdateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
        ];

        static List<ColumnInfo> BuildHistoryColumns() =>
        [
            new() { ColumnCode = "Id", IsPK = 1, ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "EndpointId", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "RequestSnapshot", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "ResponseStatus", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "ResponseHeaders", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "ResponseBody", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "DurationMs", ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
            new() { ColumnCode = "ExtractedVars", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "ValidationResults", ColumnCSharpType = "string", ColumnType = "text", IsNullable = 1 },
            new() { ColumnCode = "CreateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
            new() { ColumnCode = "UpdateTime", ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
        ];
    }
}

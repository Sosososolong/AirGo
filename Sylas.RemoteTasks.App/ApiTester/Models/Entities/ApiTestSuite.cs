using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Entities
{
    /// <summary>
    /// 测试套件 - 一批可连续执行的接口(有序), 对应某个业务功能或测试需求, 免去每次重新勾选编排
    /// </summary>
    [Table(TableName)]
    public class ApiTestSuite : EntityBase<int>
    {
        public const string TableName = "ApiTestSuites";
        /// <summary>
        /// 所属集合 Id
        /// </summary>
        public int CollectionId { get; set; }
        /// <summary>
        /// 套件名称(如: 下单流程回归)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 套件描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 有序的接口 Id 列表(JSON 数组, 如 [3,7,5])
        /// </summary>
        public string EndpointIds { get; set; } = "[]";
    }
}

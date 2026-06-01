using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Entities
{
    /// <summary>
    /// 环境 - 用于隔离不同环境(开发/测试/生产)下的变量集合, MVP 默认仅一条 default 环境
    /// </summary>
    [Table(TableName)]
    public class ApiEnvironment : EntityBase<int>
    {
        public const string TableName = "ApiEnvironments";
        /// <summary>
        /// 环境名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 是否当前激活环境
        /// </summary>
        public bool IsActive { get; set; }
    }
}

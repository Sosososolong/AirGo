using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Entities
{
    /// <summary>
    /// 环境变量 - 通过 {{varName}} 在请求中引用
    /// </summary>
    [Table(TableName)]
    public class ApiVariable : EntityBase<int>
    {
        public const string TableName = "ApiVariables";
        /// <summary>
        /// 所属环境 Id
        /// </summary>
        public int EnvironmentId { get; set; }
        /// <summary>
        /// 变量名
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 变量值
        /// </summary>
        public string Value { get; set; } = string.Empty;
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 是否密钥(列表中遮罩显示)
        /// </summary>
        public bool IsSecret { get; set; }
    }
}

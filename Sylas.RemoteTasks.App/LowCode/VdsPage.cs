using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.LowCode
{
    /// <summary>
    /// VDS页面配置实体
    /// 用于存储低代码页面的VDS配置信息
    /// </summary>
    [Table("VdsPages")]
    public class VdsPage : EntityBase<int>
    {
        /// <summary>
        /// 页面唯一标识（用于路由，如 "users"、"products"）
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 页面标题（显示用）
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 页面描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// VDS配置JSON（包含apiUrl、ths、modalSettings等完整配置）
        /// </summary>
        public string VdsConfig { get; set; } = "{}";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 排序号
        /// </summary>
        public int OrderNo { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public VdsPage() : base()
        {
        }

        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        public VdsPage(string name, string title, string description, string vdsConfig, bool isEnabled = true, int orderNo = 0) : base()
        {
            Name = name;
            Title = title;
            Description = description;
            VdsConfig = vdsConfig;
            IsEnabled = isEnabled;
            OrderNo = orderNo;
        }
    }
}

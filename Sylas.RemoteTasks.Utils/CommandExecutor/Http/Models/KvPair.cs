namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// 键值对(共享层中性版本, 兼容 ApiTester 的 KvRow 与 HttpExecutor 的 string[] Headers)
    /// </summary>
    public class KvPair
    {
        /// <summary>
        /// 键
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用, 如 HTTP 请求头中某个键值对被禁用, 则不参与请求
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 功能描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}

namespace Sylas.RemoteTasks.Utils.Dtos
{
    /// <summary>
    /// Ai配置选项
    /// </summary>
    public class AiConfig
    {
        /// <summary>
        /// 服务器地址
        /// </summary>
        public string Server { get; set; } = string.Empty;
        /// <summary>
        /// AI所使用的大模型
        /// </summary>
        public string Model { get; set; } = string.Empty;
        /// <summary>
        /// 调用AI接口需要授权的ApiKey
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}

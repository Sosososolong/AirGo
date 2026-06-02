namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// HTTP请求体的类型
    /// </summary>
    public enum BodyKind
    {
        /// <summary>
        /// 未指定 未知
        /// </summary>
        None = 0,
        /// <summary>
        /// JSON格式的请求体, 如{"name": "value"}
        /// </summary>
        Json = 1,
        /// <summary>
        /// application/x-www-form-urlencoded.
        /// </summary>
        FormUrlEncoded = 2,
        /// <summary>
        /// 表单提交
        /// </summary>
        FormData = 3,
        /// <summary>
        /// Xml数据
        /// </summary>
        Xml = 4,
        /// <summary>
        /// 文本
        /// </summary>
        Text = 5,
    }
}

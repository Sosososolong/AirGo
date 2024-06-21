namespace Sylas.RemoteTasks.Utils.Constants
{
    /// <summary>
    /// 请求头常量
    /// </summary>
    public class HeaderConstants
    {
        /// <summary>
        /// 客户端的真实IP
        /// </summary>
        public const string RealIp = "X-Real-IP";
        /// <summary>
        /// 被转发的对象
        /// </summary>
        public const string ForwardedFor = "X-Forwarded-For";
    }
}

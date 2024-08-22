namespace Sylas.RemoteTasks.Utils.Message
{
    /// <summary>
    /// 邮件发送者相关信息
    /// </summary>
    public class EmailSender
    {
        /// <summary>
        /// 收件人看到的发件人名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 授权登录其他客户端的授权码或密码
        /// </summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// 邮箱地址
        /// </summary>
        public string Address { get; set; } = string.Empty;
        /// <summary>
        /// SMTP协议服务器地址
        /// </summary>
        public string Server { get; set; } = string.Empty;
        /// <summary>
        /// SMTP协议服务端口
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// 是否使用ssl
        /// </summary>
        public bool UseSsl { get; set; } = true;
    }
}

namespace Sylas.RemoteTasks.App.Infrastructure
{
    public class AppStatus
    {
        /// <summary>
        /// 中心服务器地址(域名或IP)
        /// </summary>
        public static string CenterServer { get; set; } = string.Empty;
        /// <summary>
        /// 当前应用是否是中心服务器
        /// </summary>
        public static bool IsCenterServer { get; set; }
        /// <summary>
        /// 中心服务器的Url地址(用于Api请求)
        /// </summary>
        public static string? CenterWebServer { get; set; } = string.Empty;
        /// <summary>
        /// 当前应用所在主机的域名
        /// </summary>
        public static string Domain { get; set; } = string.Empty;
        /// <summary>
        /// 当前应用的路径
        /// </summary>
        public static string InstancePath { get; set; } = string.Empty;
        /// <summary>
        /// 当前进程的Id
        /// </summary>
        public static int ProcessId { get; set; }
    }
}

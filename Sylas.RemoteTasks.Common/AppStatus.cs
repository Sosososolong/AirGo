namespace Sylas.RemoteTasks.Common
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
        /// 当前应用(dll文件所在的)的路径的base64字符串
        /// </summary>
        public static string InstancePath { get; set; } = string.Empty;
        /// <summary>
        /// 静态目录(./wwwroot/Static)
        /// </summary>
        public static string StaticDirectory { get; set; } = string.Empty;
        /// <summary>
        /// 当前进程的Id
        /// </summary>
        public static int ProcessId { get; set; }
    }
}

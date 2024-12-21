namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class AppStatus
    {
        public static string CenterServer { get; set; } = string.Empty;
        public static bool IsCenterServer { get; set; }
        public static string? CenterWebServer { get; set; } = string.Empty;
        public static string Domain { get; set; } = string.Empty;
    }
}

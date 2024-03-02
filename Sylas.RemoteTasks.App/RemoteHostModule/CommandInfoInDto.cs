namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class CommandInfoInDto
    {
        /// <summary>
        /// AnythingSetting的Id
        /// </summary>
        public int SettingId { get; set; }
        /// <summary>
        /// 命令的名称
        /// </summary>
        public string CommandName { get; set; } = string.Empty;
    }
}

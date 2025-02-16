namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class CommandInfoTaskDto : CommandInfoInDto
    {
        /// <summary>
        /// AnythingSetting的Id
        /// </summary>
        public int SettingId { get; set; }
        /// <summary>
        /// 命令的名称
        /// </summary>
        public string CommandName { get; set; } = string.Empty;
        /// <summary>
        /// 命令所属主机域名
        /// </summary>
        public string Domain { get; set; } = string.Empty;
    }
}

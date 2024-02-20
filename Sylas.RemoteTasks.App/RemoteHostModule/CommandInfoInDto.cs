namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class CommandInfoInDto
    {
        /// <summary>
        /// 命令所属AnythingInfo对象的名称
        /// </summary>
        public string Anything { get; set; } = string.Empty;
        /// <summary>
        /// 命令的名称
        /// </summary>
        public string Command { get; set; } = string.Empty;
    }
}

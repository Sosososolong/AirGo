namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class CommandResolveDto
    {
        /// <summary>
        /// AnythingSetting的Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 要解析的命令文本
        /// </summary>
        public string CmdTxt { get; set; } = string.Empty;
    }
}

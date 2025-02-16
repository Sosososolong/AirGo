namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class CommandInfoInDto
    {
        /// <summary>
        /// AnythingSetting的Id
        /// </summary>
        public int CommandId { get; set; }
        /// <summary>
        /// 命令执行的编号, 命令结果原样返回, 用于客户端并发状态下匹配发送的命令和对应的结果
        /// </summary>
        public string CommandExecuteNo { get; set; } = string.Empty;
    }
}

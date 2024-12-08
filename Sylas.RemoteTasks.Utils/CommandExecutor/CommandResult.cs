namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 命令执行结果
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// 使用默认值实例化
        /// </summary>
        public CommandResult() { }
        /// <summary>
        /// 实例化
        /// </summary>
        /// <param name="succeed"></param>
        /// <param name="msg"></param>
        /// <param name="commandExecuteNo"></param>
        public CommandResult(bool succeed, string msg, string commandExecuteNo = "")
        {
            Succeed = succeed;
            Message = msg;
            CommandExecuteNo = commandExecuteNo;
        }
        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool Succeed { get; set; }
        /// <summary>
        /// 执行完命令返回的结果
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// 命令执行编号, 命令结果原样返回, 用于客户端并发状态下匹配发送的命令和对应的结果
        /// </summary>
        public string CommandExecuteNo { get; set; } = string.Empty;
    }
}

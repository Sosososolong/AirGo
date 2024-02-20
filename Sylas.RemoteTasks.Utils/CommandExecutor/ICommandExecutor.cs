using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 执行一些操作的抽象对象
    /// </summary>
    public interface ICommandExecutor
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public Task<CommandResult> ExecuteAsync(string command);
    }
}

using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    [Table("AnythingCommands")]
    public class AnythingCommand : EntityBase<int>
    {
        public int AnythingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CommandTxt { get; set; } = string.Empty;
        /// <summary>
        /// 命令: 获取命令执行的结果; 比如CommandTxt为启动MySQL的命令, 那么ExecuteState为MySql的运行状态查询:tasklist | findstr "mysqld.exe"
        /// </summary>
        public string ExecutedState { get; set; } = string.Empty;
        /// <summary>
        /// 命令所属主机域名
        /// </summary>
        public string Domain { get; set; } = string.Empty;
    }
}

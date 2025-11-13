using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    [Table("AnythingFlows")]
    public class AnythingFlow : EntityBase<int>
    {
        public string Title { get; set; } = string.Empty;
        public string EnvVars { get; set; } = string.Empty;
        /// <summary>
        /// 多个AnythingSetting的Id, 用逗号隔开
        /// </summary>
        public string AnythingIds { get; set; } = string.Empty;
        /// <summary>
        /// 任务调度自动执行flow
        /// </summary>
        public string Schedule { get; set; } = string.Empty;
        /// <summary>
        /// 任务调度所属主机域名, 只有相同域名下的主机才能自动执行该flow的任务调度
        /// </summary>
        public string ScheduleDomain { get; set; } = string.Empty;
        /// <summary>
        /// flow执行完毕触发
        /// </summary>
        public string OnExecuted { get; set; } = string.Empty;
    }
}

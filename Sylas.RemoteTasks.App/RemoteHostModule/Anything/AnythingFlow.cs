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
    }
}

using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class AnythingFlow : EntityBase<int>
    {
        /// <summary>
        /// 多个AnythingSetting的Id, 用逗号隔开
        /// </summary>
        public string SettingIds { get; set; } = string.Empty;
    }
}

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class AnythingSettingDetails : AnythingSetting
    {
        /// <summary>
        /// 可执行命令集合
        /// </summary>
        public IEnumerable<AnythingCommand> Commands { get; set; } = [];
    }
}

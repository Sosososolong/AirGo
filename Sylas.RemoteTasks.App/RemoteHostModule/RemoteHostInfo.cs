namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public abstract class RemoteHostInfo
    {
        public abstract string Description { get; }
        public abstract string Name { get; }
        public abstract string ShortName {  get; }
        /// <summary>
        /// 有时候一个服务器信息名称可能有简写, 名称与名称简写之间的映射字典
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract Dictionary<string, string> ShortNameMapDictionary {  get; }
        public abstract List<Tuple<string, string>> Labels { get; }
        public virtual List<CommandInfo> Commands { get; set; } = [];
        public virtual List<CommandInfo> HostInfoCommands { get; set; } = [];
    }
}

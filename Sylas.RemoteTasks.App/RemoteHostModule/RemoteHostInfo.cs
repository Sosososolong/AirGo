namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// 主机信息
    /// </summary>
    public abstract class RemoteHostInfo
    {
        /// <summary>
        /// 描述
        /// </summary>
        public abstract string Description { get; }
        /// <summary>
        /// 主机信息名称
        /// </summary>
        public abstract string Name { get; }
        /// <summary>
        /// 主机信息别名
        /// </summary>
        public abstract string ShortName {  get; }
        /// <summary>
        /// 有时候一个服务器信息名称可能有简写, 名称与名称简写之间的映射字典
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract Dictionary<string, string> ShortNameMapDictionary {  get; }
        /// <summary>
        /// 主机信息标签列表, 用于展示主机信息展示
        /// </summary>
        public abstract List<Tuple<string, string>> Labels { get; }
        /// <summary>
        /// 对当前主机信息的操作集合
        /// </summary>
        public virtual List<CommandInfo> Commands { get; set; } = [];
    }
}

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
        /// 主机信息标签列表, 用于展示主机信息展示
        /// </summary>
        public abstract List<Tuple<string, string>> Labels { get; }
        /// <summary>
        /// 对当前主机信息的操作集合
        /// </summary>
        public virtual List<CommandInfo> Commands { get; set; } = [];
    }
}

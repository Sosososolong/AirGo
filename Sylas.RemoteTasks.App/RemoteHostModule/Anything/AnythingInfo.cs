namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// 用来描述任何需要操作的对象
    /// </summary>
    public class AnythingInfo
    {

        /// <summary>
        /// 用于显示, 使用模板从属性中获取
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 可执行命令
        /// </summary>
        public IEnumerable<AnythingCommand> Commands { get; set; } = [];

        /// <summary>
        /// 给当前对象自定义属性
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = [];

        /// <summary>
        /// 配置记录的Id
        /// </summary>
        public int SettingId { get; set; }

        /// <summary>
        /// 命令执行器
        /// </summary>
        public string CommandExecutor { get; set; } = string.Empty;

        /// <summary>
        /// 执行器构造参数（从 Properties 解析而来，可安全缓存）
        /// </summary>
        public object[] ExecutorArgs { get; set; } = [];
    }
}

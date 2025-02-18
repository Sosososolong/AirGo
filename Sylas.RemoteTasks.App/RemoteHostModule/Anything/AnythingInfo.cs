using Newtonsoft.Json;
using Sylas.RemoteTasks.Utils.CommandExecutor;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// 用来描述任何需要操作的对象
    /// </summary>
    public class AnythingInfo
    {
        /// <summary>
        /// 标识, 使用模板从属性中获取
        /// </summary>
        public string Name { get; set; } = string.Empty;

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
        public int SettingId { get; set; }

        public string CommandExecutor { get; set; } = string.Empty;
    }
}

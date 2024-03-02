using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// AnythingInfo的本文配置
    /// </summary>
    public class AnythingSetting : EntityBase<int>
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
        public string Commands { get; set; } = string.Empty;

        /// <summary>
        /// 给当前对象自定义属性
        /// </summary>
        public string Properties { get; set; } = string.Empty;

        public int Executor { get; set; }
    }
}

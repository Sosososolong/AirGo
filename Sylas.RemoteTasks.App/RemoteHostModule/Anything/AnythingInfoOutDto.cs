namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class AnythingInfoOutDto
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
        public List<string> Commands { get; set; } = [];
    }
}

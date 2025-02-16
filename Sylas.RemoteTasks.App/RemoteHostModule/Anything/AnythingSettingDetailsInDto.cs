namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class AnythingSettingDetailsInDto
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
        public string Properties { get; set; } = string.Empty;

        public int Executor { get; set; }
        public AnythingSetting ToAnythingSetting()
        {
            return new AnythingSetting
            {
                Name = Name,
                Title = Title,
                Properties = Properties,
                Executor = Executor
            };
        }
    }
}

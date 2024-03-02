namespace Sylas.RemoteTasks.Utils.Dto
{
    /// <summary>
    /// 描述参数信息的基类
    /// </summary>
    public class ArgumentInfo
    {
        /// <summary>
        /// 参数值
        /// </summary>
        public string ArgumentValue { get; set; } = string.Empty;
        /// <summary>
        /// 参数类型
        /// </summary>
        public string? ArgumentType { get; set; }
    }
}

namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 文本被解析成的子文本片段有哪些内容
    /// </summary>
    public enum TextBlocksType
    {
        /// <summary>
        /// 包含了文本所有内容
        /// </summary>
        All,
        /// <summary>
        /// 只包含具有指定开始和结束标识的子文本片段
        /// </summary>
        Specific,
    }
}

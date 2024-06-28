namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 行和对应的子文本片段
    /// </summary>
    /// <remarks>
    /// 初始化对象
    /// </remarks>
    /// <param name="line"></param>
    /// <param name="block"></param>
    public class TextLineTextBlock(TextLine line, TextBlock? block)
    {
        /// <summary>
        /// 文本行信息
        /// </summary>
        public TextLine Line { get; set; } = line;
        /// <summary>
        /// 行所在的子文本片段, 不在任何文本片段中则为空
        /// </summary>
        public TextBlock? Block { get; set; } = block;
    }
}

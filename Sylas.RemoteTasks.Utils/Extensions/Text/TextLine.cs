namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 描述模板中的一行
    /// </summary>
    public class TextLine
    {
        /// <summary>
        /// 初始化默认值
        /// </summary>
        public TextLine()
        {
            LineIndex = -1;
            Content = string.Empty;
        }
        /// <summary>
        /// 初始化行索引和原始值
        /// </summary>
        /// <param name="index"></param>
        /// <param name="origin"></param>
        public TextLine(int index, string origin)
        {
            LineIndex = index;
            Content = origin;
        }
        /// <summary>
        /// 行的索引
        /// </summary>
        public int LineIndex { get; set; }
        /// <summary>
        /// 行的原始内容
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// 方便显示行信息
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            //return $"{LineIndex}: {(Content.Length > 20 ? Content[..10] + Content.Substring(10, 7) : Content)}";
            return $"{LineIndex}: {Content}";
        }
    }
}

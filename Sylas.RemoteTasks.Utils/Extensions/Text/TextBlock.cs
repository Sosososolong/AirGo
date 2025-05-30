using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 模板中的文本片段
    /// </summary>
    public class TextBlock : List<TextLine>
    {
        /// <summary>
        /// 嵌套的子文本片段
        /// </summary>
        public List<TextBlock> Children { get; set; } = [];
        /// <summary>
        /// 添加一行
        /// </summary>
        /// <param name="index"></param>
        /// <param name="content"></param>
        public void AddLine(int index, string content)
        {
            Add(new(index, content));
        }
        /// <summary>
        /// 文本块第一行索引
        /// </summary>
        public int FirstLine => Count > 0 ? this[0].LineIndex : -1;
        /// <summary>
        /// 文本块最后一行索引
        /// </summary>
        public int LastLine => Count > 0 ? this[^1].LineIndex : -1;
    }
}

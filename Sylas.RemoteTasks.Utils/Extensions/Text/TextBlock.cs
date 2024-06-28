using System.Collections;
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
    }
}

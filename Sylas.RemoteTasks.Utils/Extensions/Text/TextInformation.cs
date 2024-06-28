using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 文本解析的结果
    /// </summary>
    public class TextInformation
    {

        /// <summary>
        /// 文本中所有具有明确开始结束标识的子文本片段
        /// </summary>
        public List<TextBlock> Blocks { get; set; } = [];
    }
}

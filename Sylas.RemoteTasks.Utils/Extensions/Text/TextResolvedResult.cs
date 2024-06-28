using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 文本解析后的结果
    /// </summary>
    /// <remarks>
    /// 初始化
    /// </remarks>
    /// <param name="specifiedBlocks"></param>
    /// <param name="sequenceLineInfos"></param>
    public class TextResolvedResult(List<TextBlock> specifiedBlocks, List<TextLineTextBlock> sequenceLineInfos)
    {
        /// <summary>
        /// 指定开始和结束符的所有文本片段(具有上下层级的嵌套结构)
        /// </summary>
        public List<TextBlock> SpecifiedBlocks { get; set; } = specifiedBlocks;

        /// <summary>
        /// 有序的所有行(和所在的文本片段信息)信息
        /// </summary>
        public List<TextLineTextBlock> SequenceLineInfos { get; set; } = sequenceLineInfos;
    }
}

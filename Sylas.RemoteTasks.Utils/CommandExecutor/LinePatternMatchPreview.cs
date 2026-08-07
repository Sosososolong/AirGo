using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// LinePattern匹配范围
    /// </summary>
    public static class LinePatternMatchScopes
    {
        /// <summary>
        /// 按行匹配(Append/Prepend: 内容按行分割后逐行匹配)
        /// </summary>
        public const string Line = "Line";
        /// <summary>
        /// 全文匹配(Replace: 在整个文件内容上匹配, 允许跨行)
        /// </summary>
        public const string Content = "Content";
        /// <summary>
        /// 不使用LinePattern(Override/Create, 或者Append/Prepend没有配置LinePattern)
        /// </summary>
        public const string None = "None";
    }

    /// <summary>
    /// LinePattern匹配预览: 一个操作步骤的正则在一个目标文件中匹配到的所有内容(只读取文件, 不做任何修改)
    /// </summary>
    public class LinePatternMatchPreview
    {
        /// <summary>
        /// 目标文件完整路径
        /// </summary>
        public string File { get; set; } = string.Empty;
        /// <summary>
        /// 操作节点标题(###后面的标题)
        /// </summary>
        public string NodeTitle { get; set; } = string.Empty;
        /// <summary>
        /// 操作类型: Append/Prepend/Replace/Override/Create
        /// </summary>
        public string OperationType { get; set; } = string.Empty;
        /// <summary>
        /// 已完成变量解析的LinePattern(真实执行时使用的正则)
        /// </summary>
        public string LinePattern { get; set; } = string.Empty;
        /// <summary>
        /// 匹配范围, 取值见<see cref="LinePatternMatchScopes"/>
        /// </summary>
        public string MatchScope { get; set; } = string.Empty;
        /// <summary>
        /// 所有匹配项
        /// </summary>
        public List<LinePatternMatch> Matches { get; set; } = [];
        /// <summary>
        /// 正则无效或没有匹配到内容时的错误信息(真实执行会失败或者不产生改动)
        /// </summary>
        public string Error { get; set; } = string.Empty;
        /// <summary>
        /// 匹配行为的补充说明
        /// </summary>
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// LinePattern的一处匹配
    /// </summary>
    public class LinePatternMatch
    {
        /// <summary>
        /// 匹配内容的起始行号(1开始; 全文匹配时匹配内容可能跨多行)
        /// </summary>
        public int LineNumber { get; set; }
        /// <summary>
        /// 匹配到的内容
        /// </summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>
        /// 真实执行时是否会使用这一处匹配(Append/Prepend只使用一处作为定位锚点, Replace会使用全部匹配)
        /// </summary>
        public bool IsAnchor { get; set; }
    }
}

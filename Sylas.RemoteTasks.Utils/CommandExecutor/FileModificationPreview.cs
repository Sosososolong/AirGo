using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 文件修改预览: 一个操作步骤对一个文件的改动情况(不落盘, 与真实执行使用同一套计算逻辑)
    /// </summary>
    public class FileModificationPreview
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
        /// 该步骤是否会产生改动(false表示已经是目标状态, 真实执行时会跳过)
        /// </summary>
        public bool Changed { get; set; }
        /// <summary>
        /// 目标文件当前是否不存在(真实执行时会新建)
        /// </summary>
        public bool IsNewFile { get; set; }
        /// <summary>
        /// 与真实执行完全一致的操作日志
        /// </summary>
        public string Log { get; set; } = string.Empty;
        /// <summary>
        /// 改动前后的差异块(只包含有改动的部分及其上下文)
        /// </summary>
        public List<DiffHunk> Hunks { get; set; } = [];
    }

    /// <summary>
    /// 一段连续的差异块(包含上下文行)
    /// </summary>
    public class DiffHunk
    {
        /// <summary>
        /// 差异块在改动前内容中的起始行号(1开始)
        /// </summary>
        public int OriginStart { get; set; }
        /// <summary>
        /// 差异块包含的改动前的行数
        /// </summary>
        public int OriginCount { get; set; }
        /// <summary>
        /// 差异块在改动后内容中的起始行号(1开始)
        /// </summary>
        public int NewStart { get; set; }
        /// <summary>
        /// 差异块包含的改动后的行数
        /// </summary>
        public int NewCount { get; set; }
        /// <summary>
        /// 差异块中的所有行
        /// </summary>
        public List<DiffLine> Lines { get; set; } = [];
    }

    /// <summary>
    /// 差异块中的一行
    /// </summary>
    public class DiffLine
    {
        /// <summary>
        /// 行类型: equal-未改动的上下文行; del-改动前删除的行; add-改动后新增的行
        /// </summary>
        public string Kind { get; set; } = string.Empty;
        /// <summary>
        /// 改动前的行号(add类型没有值)
        /// </summary>
        public int? OriginLineNumber { get; set; }
        /// <summary>
        /// 改动后的行号(del类型没有值)
        /// </summary>
        public int? NewLineNumber { get; set; }
        /// <summary>
        /// 行内容
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }
}

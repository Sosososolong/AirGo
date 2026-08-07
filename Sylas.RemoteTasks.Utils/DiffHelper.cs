using Sylas.RemoteTasks.Utils.CommandExecutor;
using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 文本差异对比帮助类: 按行对比两段文本, 输出只包含改动部分及其上下文的差异块
    /// </summary>
    public static class DiffHelper
    {
        /// <summary>
        /// 差异行的类型
        /// </summary>
        enum DiffOp
        {
            /// <summary>未改动</summary>
            Equal,
            /// <summary>改动前存在, 改动后被删除</summary>
            Delete,
            /// <summary>改动后新增</summary>
            Insert
        }

        /// <summary>
        /// LCS动态规划矩阵的规模上限, 超过则退化为整块替换(避免超大文件对比消耗过多内存)
        /// </summary>
        const long MaxLcsMatrixSize = 4_000_000;

        /// <summary>
        /// 对比两段文本, 获取差异块
        /// </summary>
        /// <param name="origin">改动前的内容</param>
        /// <param name="modified">改动后的内容</param>
        /// <param name="contextLines">差异行前后保留的上下文行数</param>
        /// <returns>差异块集合, 内容完全相同时返回空集合</returns>
        public static List<DiffHunk> BuildHunks(string origin, string modified, int contextLines = 3)
        {
            string[] originLines = SplitLines(origin);
            string[] newLines = SplitLines(modified);

            // 裁剪公共前缀和公共后缀, 只对中间的差异区域做LCS, 避免大文件小改动时的性能问题
            int prefix = 0;
            while (prefix < originLines.Length && prefix < newLines.Length && originLines[prefix] == newLines[prefix])
            {
                prefix++;
            }
            int originEnd = originLines.Length - 1;
            int newEnd = newLines.Length - 1;
            while (originEnd >= prefix && newEnd >= prefix && originLines[originEnd] == newLines[newEnd])
            {
                originEnd--;
                newEnd--;
            }

            // 完全相同
            if (prefix > originEnd && prefix > newEnd)
            {
                return [];
            }

            List<DiffLine> allLines = [];
            int originLineNo = 0;
            int newLineNo = 0;

            // 公共前缀
            for (int i = 0; i < prefix; i++)
            {
                allLines.Add(new DiffLine { Kind = "equal", OriginLineNumber = ++originLineNo, NewLineNumber = ++newLineNo, Text = originLines[i] });
            }

            // 中间差异区域
            string[] middleOrigin = Slice(originLines, prefix, originEnd);
            string[] middleNew = Slice(newLines, prefix, newEnd);
            foreach (var (op, text) in DiffMiddle(middleOrigin, middleNew))
            {
                if (op == DiffOp.Equal)
                {
                    allLines.Add(new DiffLine { Kind = "equal", OriginLineNumber = ++originLineNo, NewLineNumber = ++newLineNo, Text = text });
                }
                else if (op == DiffOp.Delete)
                {
                    allLines.Add(new DiffLine { Kind = "del", OriginLineNumber = ++originLineNo, NewLineNumber = null, Text = text });
                }
                else
                {
                    allLines.Add(new DiffLine { Kind = "add", OriginLineNumber = null, NewLineNumber = ++newLineNo, Text = text });
                }
            }

            // 公共后缀
            for (int i = originEnd + 1; i < originLines.Length; i++)
            {
                allLines.Add(new DiffLine { Kind = "equal", OriginLineNumber = ++originLineNo, NewLineNumber = ++newLineNo, Text = originLines[i] });
            }

            return GroupHunks(allLines, contextLines);
        }

        /// <summary>
        /// 按行分割文本, 统一换行符(避免CRLF与LF的差异被识别为整个文件改动)
        /// </summary>
        static string[] SplitLines(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return [];
            }
            return content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        static string[] Slice(string[] source, int start, int end)
        {
            if (end < start)
            {
                return [];
            }
            var result = new string[end - start + 1];
            Array.Copy(source, start, result, 0, result.Length);
            return result;
        }

        /// <summary>
        /// 对差异区域求最长公共子序列, 得到最小改动集
        /// </summary>
        static List<(DiffOp Op, string Text)> DiffMiddle(string[] originLines, string[] newLines)
        {
            List<(DiffOp, string)> result = [];
            int m = originLines.Length;
            int n = newLines.Length;
            if (m == 0 && n == 0)
            {
                return result;
            }
            // 一侧为空, 或者规模过大退化为整块替换
            if (m == 0 || n == 0 || (long)(m + 1) * (n + 1) > MaxLcsMatrixSize)
            {
                foreach (var line in originLines)
                {
                    result.Add((DiffOp.Delete, line));
                }
                foreach (var line in newLines)
                {
                    result.Add((DiffOp.Insert, line));
                }
                return result;
            }

            // lcs[i,j]: originLines[i..]与newLines[j..]的最长公共子序列长度
            var lcs = new int[m + 1, n + 1];
            for (int i = m - 1; i >= 0; i--)
            {
                for (int j = n - 1; j >= 0; j--)
                {
                    lcs[i, j] = originLines[i] == newLines[j]
                        ? lcs[i + 1, j + 1] + 1
                        : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }

            int x = 0, y = 0;
            while (x < m && y < n)
            {
                if (originLines[x] == newLines[y])
                {
                    result.Add((DiffOp.Equal, originLines[x]));
                    x++;
                    y++;
                }
                else if (lcs[x + 1, y] >= lcs[x, y + 1])
                {
                    result.Add((DiffOp.Delete, originLines[x]));
                    x++;
                }
                else
                {
                    result.Add((DiffOp.Insert, newLines[y]));
                    y++;
                }
            }
            while (x < m)
            {
                result.Add((DiffOp.Delete, originLines[x++]));
            }
            while (y < n)
            {
                result.Add((DiffOp.Insert, newLines[y++]));
            }
            return result;
        }

        /// <summary>
        /// 将所有行按改动位置分组, 每组保留前后contextLines行上下文; 相距很近的改动合并到同一个块中
        /// </summary>
        static List<DiffHunk> GroupHunks(List<DiffLine> allLines, int contextLines)
        {
            List<DiffHunk> hunks = [];
            int index = 0;
            while (index < allLines.Count)
            {
                if (allLines[index].Kind == "equal")
                {
                    index++;
                    continue;
                }

                // 向后扫描到本块的最后一个改动行(中间相等行不超过2倍上下文则合并为同一个块)
                int lastChange = index;
                int scan = index;
                while (scan < allLines.Count)
                {
                    if (allLines[scan].Kind != "equal")
                    {
                        lastChange = scan;
                    }
                    else if (scan - lastChange > contextLines * 2)
                    {
                        break;
                    }
                    scan++;
                }

                int hunkStart = Math.Max(0, index - contextLines);
                int hunkEnd = Math.Min(allLines.Count - 1, lastChange + contextLines);

                DiffHunk hunk = new();
                for (int i = hunkStart; i <= hunkEnd; i++)
                {
                    DiffLine line = allLines[i];
                    hunk.Lines.Add(line);
                    if (line.OriginLineNumber.HasValue)
                    {
                        if (hunk.OriginStart == 0)
                        {
                            hunk.OriginStart = line.OriginLineNumber.Value;
                        }
                        hunk.OriginCount++;
                    }
                    if (line.NewLineNumber.HasValue)
                    {
                        if (hunk.NewStart == 0)
                        {
                            hunk.NewStart = line.NewLineNumber.Value;
                        }
                        hunk.NewCount++;
                    }
                }
                hunks.Add(hunk);
                index = hunkEnd + 1;
            }
            return hunks;
        }
    }
}

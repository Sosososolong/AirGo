using System;
using System.Collections.Generic;
using System.Linq;

namespace Sylas.RemoteTasks.Utils.Extensions.Text
{
    /// <summary>
    /// 文本分析工具
    /// </summary>
    public static class TextExtensions
    {
        /// <summary>
        /// 获取文本中的有明确开始和结束标识的子文本片段
        /// </summary>
        /// <param name="source">要分析的文本</param>
        /// <param name="startFlag">开始标识</param>
        /// <param name="endFlag">结束标识</param>
        public static TextResolvedResult GetBlocks(this string source, string startFlag, string endFlag)
        {
            var lineArray = source.Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
            return GetAllBlocksAndAllLines(lineArray, startFlag, endFlag);
        }
        /// <summary>
        /// 获取文本中的有明确开始和结束标识的子文本片段(保持上下层级的嵌套关系) 和 按顺序排序的所有行(以及所在文本片段)信息
        /// </summary>
        /// <param name="lineArray">要分析的文本所有行</param>
        /// <param name="startFlag">开始标识</param>
        /// <param name="endFlag">结束标识</param>
        public static TextResolvedResult GetAllBlocksAndAllLines(this string[] lineArray, string startFlag, string endFlag)
        {
            List<TextLineTextBlock> sequenceLines = [];
            List<TextBlock> allBlocks = [];

            // 遇到新的TextBlock就加进去, 遇到TextBlock结束标识就移除当前TextBlock(同级的TextBlock只会存在一个, 上一个TextBlock是下一个的父级)
            List<TextBlock> blockRecords = [];

            for (int i = 0; i < lineArray.Length; i++)
            {
                string readingLine = lineArray[i];
                TextLine lineInfo = new(i, readingLine);
                TextBlock? lineBlock = null;
                if (blockRecords.Count == 0)
                {
                    if (IsBlockStartLine(lineInfo.Content, startFlag))
                    {
                        // BOOKMARK: 当前行属于新的文本片段(最外层第一行)
                        lineBlock = [lineInfo];
                        blockRecords.Add(lineBlock);
                    }
                }
                else
                {
                    if (IsBlockStartLine(lineInfo.Content, startFlag))
                    {
                        // BOOKMARK: 当前行属于新的文本片段(嵌套的第一行)
                        lineBlock = [lineInfo];
                        blockRecords.Add(lineBlock);
                    }
                    else
                    {
                        // BOOKMARK: 当前行的文本片段
                        lineBlock = blockRecords.LastOrDefault() ?? throw new Exception("属于文本片段中的行找不到它的文本片段");
                        lineBlock.Add(lineInfo);


                        if (IsBlockEndLine(lineInfo.Content, endFlag))
                        {
                            int parentBlockIndex = blockRecords.IndexOf(lineBlock) - 1;
                            if (parentBlockIndex >= 0)
                            {
                                TextBlock parentBlock = blockRecords[parentBlockIndex];
                                parentBlock.Children.Add(lineBlock);
                            }
                            else
                            {
                                allBlocks.Add(lineBlock);
                            }
                            blockRecords.Remove(lineBlock);
                        }
                    }
                }

                sequenceLines.Add(new(lineInfo, lineBlock));
            }
            return new(allBlocks, sequenceLines);
            //List<TextBlock> ResolveBlocksRecursively(int start)
            //{
            //    List<TextBlock> textBlocks = [];
            //    bool lineInBlock = false;

            //    TextBlock block = [];
            //    for (int i = start; i < allLinesCount; i++)
            //    {
            //        string readingLine = lineArray[i];
            //        TextLine lineInfo = new(i, readingLine);
            //        if (!lineInBlock)
            //        {
            //            if (IsBlockStartLine(lineInfo.Content, startFlag))
            //            {
            //                // BOOKMARK: 获取指定的子文本片段 - 1. 开始时添加第一行
            //                lineInBlock = true;
            //                if (block.Count > 0)
            //                {
            //                    block = [];
            //                }

            //                block.Add(lineInfo);

            //                // BOOKMARK: 所有行并关联文本片段 - 1 子文本片段第一行
            //                sequenceLines.Add(new(lineInfo, block));
            //            }
            //            else
            //            {
            //                // BOOKMARK: 所有行并关联文本片段 - 2 子文本片段之外的行
            //                sequenceLines.Add(new(lineInfo, null));
            //            }
            //        }
            //        else
            //        {
            //            // BOOKMARK: 所有行并关联文本片段 第一行已处理(1)
            //            if (IsBlockStartLine(lineInfo.Content, startFlag))
            //            {
            //                // BOOKMARK: 获取指定的子文本片段 - 2.1 for循环体中的行是内嵌的for循环, 递归处理
            //                List<TextBlock> childBlocks = ResolveBlocksRecursively(i);
            //                block.Children.AddRange(childBlocks);

            //                // 更新当前读取的行索引
            //                i = childBlocks.Last().Last().LineIndex;
            //            }
            //            else
            //            {
            //                // BOOKMARK: 获取指定的子文本片段 - 2.2 添加普通的for循环体中的行(同时需要检查是否到达结束标识)
            //                block.Add(lineInfo);

            //                // BOOKMARK: 所有行并关联文本片段 - 3 指定文本片段中的其他行(包含最后一行)
            //                sequenceLines.Add(new(lineInfo, block));

            //                if (IsBlockEndLine(lineInfo.Content, endFlag))
            //                {
            //                    // 值为false, 下次解析到开头表示将重置block对象
            //                    lineInBlock = false;
            //                    textBlocks.Add(block);
            //                }
            //            }
            //        }
            //    }
            //    return textBlocks;
            //}
        }

        //// 获取扁平的, 按顺序的TextBlocks流
        //public static List<TextBlock> GetFlatBlockSequence(int maxIndex, List<TextBlock> specifiedBlocks)
        //{
        //    List<TextBlock> result = [];
        //    TextBlock handingBlock = specifiedBlocks.First();
        //    for (int i = 0; i <= maxIndex; i++)
        //    {
        //        if (handingBlock.Any())
        //        {
        //            var first = handingBlock.First();
        //            if (i < first.LineIndex)
        //            {
        //                // 处理block前面的行
        //                for (int j = i; j < first.LineIndex; j++)
        //                {
        //                    result.Add()
        //                }
        //            }
        //        }
        //    }
        //    void GetAllPlatBlocksRecursively()
        //    {

        //    }
        //}

        /// <summary>
        /// 是否是块的开始行
        /// </summary>
        /// <param name="line"></param>
        /// <param name="startFlag"></param>
        /// <returns></returns>
        static bool IsBlockStartLine(string line, string startFlag) => line.Contains($"{startFlag} ");

        static bool IsBlockEndLine(string line, string endFlag) => line.Trim() == endFlag;
    }
}

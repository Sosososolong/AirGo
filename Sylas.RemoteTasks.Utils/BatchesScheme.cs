using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 多批次处理大量工作的方案
    /// </summary>
    public class BatchesScheme
    {
        /// <summary>
        /// 执行CPU密集型任务
        /// </summary>
        /// <param name="target"></param>
        /// <param name="handleBatch"></param>
        /// <returns></returns>
        public static async Task CpuTasksExecuteAsync(IEnumerable<IDictionary<string, object>> target, Action<IEnumerable<IDictionary<string, object>>> handleBatch)
        {
            var tasks = new List<Task>();
            var batches = new List<List<IDictionary<string, object>>>();
            var batchIndex = 0;

            double processorCount = Environment.ProcessorCount;
            var batchSize = Math.Ceiling(target.Count() / processorCount);
            LoggerHelper.LogInformation($"CPU密集型任务, 将数据集拆分成({processorCount})份同时执行; Size: {batchSize}");

            var targetList = target.ToList();
            for (int i = 0; i < targetList.Count; i++)
            {
                var current = targetList[i];
                if (batches.Count < batchIndex + 1)
                {
                    batches.Add([]);
                }
                if (batches[batchIndex].Count < batchSize)
                {
                    batches[batchIndex].Add(current);
                }
                else
                {
                    batchIndex++;
                    i--;
                }
            }

            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                Task task = Task.Run(() => handleBatch(batch));
                tasks.Add(task);
            }

            LoggerHelper.LogInformation($"等待所有任务完成");
            var start = DateTime.Now;
            await Task.WhenAll(tasks);
            var end = DateTime.Now;
            LoggerHelper.LogInformation($"所有任务均已完成, 耗时: {DateTimeHelper.FormatSeconds((end - start).TotalSeconds)}");
        }
    }
}

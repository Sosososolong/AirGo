using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 多批次处理大量工作的方案
    /// </summary>
    public class BatchesSchedule
    {
        /// <summary>
        /// 执行CPU密集型任务
        /// </summary>
        /// <param name="target"></param>
        /// <param name="handleBatch"></param>
        /// <returns></returns>
        public static async Task CpuTasksExecuteAsync(IEnumerable<object> target, Action<IEnumerable<object>> handleBatch)
        {
            var tasks = new List<Task>();
            var batches = new List<List<object>>();
            var batchIndex = 0;

            double processorCount = Environment.ProcessorCount;
            var batchSize = Math.Ceiling(target.Count() / processorCount);

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

            await Task.WhenAll(tasks);
        }
    }
}

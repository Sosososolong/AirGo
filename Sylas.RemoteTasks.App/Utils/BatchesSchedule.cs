namespace Sylas.RemoteTasks.App.Utils
{
    public class BatchesSchedule
    {
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
                    batches.Add(new List<object>());
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

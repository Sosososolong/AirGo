using System.Data;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class PagedData
    {
        public int Count { get; set; }
        public IEnumerable<object> Data { get; set; } = Enumerable.Empty<object>();
        public IDataReader? DataReader { get; set; }
    }
}

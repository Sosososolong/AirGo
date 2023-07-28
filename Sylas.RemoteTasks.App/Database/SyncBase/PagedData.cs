using System.Data;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class PagedData
    {
        public int Count { get; set; }
        public IEnumerable<object> Data { get; set; } = Enumerable.Empty<object>();
        public IDataReader? DataReader { get; set; }
    }

    public class PagedData<T>
    {
        public int Count { get; set; }
        public int TotalPages { get; set; }
        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    }
}

namespace Sylas.RemoteTasks.App.Utils
{
    public static class IEnumerableExtensions
    {
        public static bool TryTake<T>(this List<T> source, out T? first)
        {
            first = source.FirstOrDefault();
            if (first is not null)
            {
                source.Remove(first);
                return true;
            }
            return false;
        }
    }
}

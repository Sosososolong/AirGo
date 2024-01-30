using System.Collections.Generic;
using System.Linq;

namespace Sylas.RemoteTasks.Utils.Extensions
{
    /// <summary>
    /// 对IEnumerable进行扩展
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// 尝试从集合中获取某个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="first"></param>
        /// <returns></returns>
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

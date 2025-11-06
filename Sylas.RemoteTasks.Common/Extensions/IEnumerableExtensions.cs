using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Sylas.RemoteTasks.Common.Extensions
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

        /// <summary>
        /// 对象集合转换为字典集合
        /// </summary>
        /// <param name="objects"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<IDictionary<string, object?>> CastToDictionaries(this IEnumerable<object> objects)
        {
            return objects.Select(x => x.CastToDictionary()).ToList();
        }
        /// <summary>
        /// 将集合分块(分页)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public static List<List<T>> ChunkData<T>(this IEnumerable<T> source, int pageSize)
        {
            List<List<T>> chunkedData = [];
            int pageIndex = 1;
            while (true)
            {
                var item = source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                chunkedData.Add(item);
                if (item.Count < pageSize)
                {
                    break;
                }
                pageIndex++;
            }
            return chunkedData;
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;

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

        /// <summary>
        /// 对象集合转换为字典集合
        /// </summary>
        /// <param name="objects"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<IDictionary<string, object>> CastToDictionaries(this IEnumerable<object> objects)
        {
            IEnumerable<IDictionary<string, object>> batchDictionaries = [];

            if (objects.Any())
            {
                var first = objects.First();
                if (first is DataRow firstRow)
                {
                    var columns = firstRow.Table.Columns;
                    batchDictionaries = objects.Cast<DataRow>().Select(record =>
                    {
                        Dictionary<string, object> recordDictionary = [];
                        foreach (DataColumn column in columns)
                        {
                            recordDictionary[column.ColumnName] = record[column];
                        }
                        return recordDictionary;
                    });
                }
                else if (first is Dictionary<string, object>)
                {
                    batchDictionaries = objects.Cast<Dictionary<string, object>>();
                }
                else if (first is IDictionary<string, object>)
                {
                    batchDictionaries = objects.Cast<IDictionary<string, object>>();
                }
                else if (first is JsonElement)
                {
                    batchDictionaries = objects.Cast<JsonElement>().Select(x =>
                    {
                        var rawText = x.GetRawText();
                        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawText) ?? throw new Exception("JsonElement转换为字典失败");
                        return dictionary;
                    });
                }
                else
                {
                    batchDictionaries = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(objects)) ?? throw new Exception("对象集合转换为字典结合失败");
                }
            }
            return batchDictionaries;
        }
    }
}

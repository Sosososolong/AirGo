using System;
using System.Collections.Generic;
using System.Linq;

namespace Sylas.RemoteTasks.Common.Extensions
{
    /// <summary>
    /// 字典比较器
    /// </summary>
    /// <param name="equals"></param>
    /// <param name="getHashCode"></param>
    public class DictionaryComparer(Func<IDictionary<string, object>, IDictionary<string, object>, bool>? equals = null, Func<IDictionary<string, object>, int>? getHashCode = null) : IEqualityComparer<IDictionary<string, object>>
    {
        /// <summary>
        /// 两个字典相等的逻辑
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool Equals(IDictionary<string, object> x, IDictionary<string, object> y)
        {
            return equals is null ? DefaultEquals(x, y) : equals(x, y);
        }

        /// <summary>
        /// 获取字典的HashCode逻辑
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int GetHashCode(IDictionary<string, object> obj)
        {
            return getHashCode is null ? DefaultGetHashCode(obj) : getHashCode(obj);
        }

        private bool DefaultEquals(IDictionary<string, object> x, IDictionary<string, object> y)
        {
            if (x == y) return true;
            if (x == null || y == null) return false;
            if (x.Count != y.Count) return false;

            foreach (var kvp in x)
            {
                if (!y.TryGetValue(kvp.Key, out var value) || !Equals(kvp.Value, value))
                {
                    return false;
                }
            }
            return true;
        }
        private int DefaultGetHashCode(IDictionary<string, object> obj)
        {
            int hash = 17;
            foreach (var kvp in obj)
            {
                hash = hash * 23 + (kvp.Key?.GetHashCode() ?? 0);
                hash = hash * 23 + (kvp.Value?.GetHashCode() ?? 0);
            }
            return hash;
        }

        /// <summary>
        /// 通过主键比较两个字典是否相等
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="sourcePrimaryKeys"></param>
        /// <returns></returns>
        public static bool EqualsByPrimaryKeys(IDictionary<string, object> x, IDictionary<string, object> y, IEnumerable<string> sourcePrimaryKeys)
        {
            // target 主键值; 支持多个
            foreach (var pk in sourcePrimaryKeys)
            {
                var xkey = x.Keys.FirstOrDefault(k => k.Equals(pk, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(xkey))
                {
                    return false;
                }
                var ykey = y.Keys.FirstOrDefault(k => k.Equals(pk, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"字典中未找到主键{pk}对应的key");
                if (string.IsNullOrWhiteSpace(ykey))
                {
                    return false;
                }
                if (!x[xkey].Equals(y[ykey]))
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 通过主键获取HashCode
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="primaryKeys"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static int GetHashCodeByPrimaryKeys(IDictionary<string, object> obj, IEnumerable<string> primaryKeys)
        {
            int hash = 17;
            foreach (var key in primaryKeys)
            {
                var k = obj.Keys.FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(k))
                {
                    throw new Exception($"主键{key}不存在");
                }
                hash = hash * 23 + (obj[k]?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}

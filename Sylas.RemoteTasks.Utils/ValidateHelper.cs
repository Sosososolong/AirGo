using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 校验帮助类
    /// </summary>
    public class ValidateHelper
    {
        /// <summary>
        /// 校验对象的每个属性是否为null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="excludeProps">不需要校验的属性</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void ValidateArgumentIsNull<T>(T? instance, List<string> excludeProps)
        {
            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            var props = instance.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (excludeProps.Contains(prop.Name))
                {
                    continue;
                }
                _ = prop.GetValue(instance, null) ?? throw new ArgumentNullException(prop.Name);
            }
        }
    }
}

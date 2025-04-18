using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Sylas.RemoteTasks.Common
{
    /// <summary>
    /// 对象转化/复制帮助类
    /// </summary>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    public static class MapHelper<TIn, TOut>
    {
        /// <summary>
        /// 其结果被赋值给静态只读字段cache。因此，GetFunc()只会在类第一次被访问时被调用一次
        /// </summary>
        private static readonly Func<TIn, TOut> _cache = GetFunc();
        private static Func<TIn, TOut> GetFunc()
        {
            ParameterExpression parameterExpression = Expression.Parameter(typeof(TIn), "p");
            List<MemberBinding> memberBindingList = [];

            foreach (var item in typeof(TOut).GetProperties())
            {
                if (!item.CanWrite)
                {
                    continue;
                }
                // 相当于表达式: p.属性x
                MemberExpression property = Expression.Property(parameterExpression, typeof(TIn).GetProperty(item.Name) ?? throw new Exception($"没有找到{typeof(TIn).Name}的{item.Name}属性"));
                // 相当于: TOut属性x = p.属性x
                MemberBinding memberBinding = Expression.Bind(item, property);
                memberBindingList.Add(memberBinding);
            }

            MemberInitExpression memberInitExpression = Expression.MemberInit(Expression.New(typeof(TOut)), [.. memberBindingList]);
            Expression<Func<TIn, TOut>> lambda = Expression.Lambda<Func<TIn, TOut>>(memberInitExpression, new ParameterExpression[] { parameterExpression });
            return lambda.Compile();
        }
        /// <summary>
        /// 从TIn对象获取TOut对象
        /// </summary>
        /// <param name="tIn"></param>
        /// <returns></returns>
        public static TOut Map(TIn tIn)
        {
            return _cache(tIn);
        }
    }
}

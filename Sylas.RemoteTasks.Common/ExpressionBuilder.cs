using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Sylas.RemoteTasks.Common
{
    /// <summary>
    /// 表达式目录树扩展类
    /// </summary>
    public static class ExpressionBuilder
    {
        /// <summary>
        /// 动态构建过滤表达式过滤数据集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="filterTemplate"></param>
        /// <returns></returns>
        public static IEnumerable<T> ApplyFilter<T>(IEnumerable<T> collection, string filterTemplate)
        {
            var filterExpression = BuildFilterExpression<T>(filterTemplate);
            return collection.Where(filterExpression.Compile());
        }
        /// <summary>
        /// 构建过滤表达式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filterTemplate"></param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> BuildFilterExpression<T>(string filterTemplate)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var expression = ParseFilterExpression(filterTemplate, parameter);
            var filterExpression = Expression.Lambda<Func<T, bool>>(expression, parameter);
            return filterExpression;
        }
        /// <summary>
        /// 构建属性表达式
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="propertyPath"></param>
        /// <returns></returns>
        public static Expression<Func<T, TResult>> BuildPropertyExpression<T, TResult>(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var propertyExpression = GetPropertyExpression(propertyPath, parameter);
            var lambdaExpression = Expression.Lambda<Func<T, TResult>>(propertyExpression, parameter);
            return lambdaExpression;
        }
        /// <summary>
        /// 转换过滤表达式
        /// </summary>
        /// <param name="filterTemplate"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Expression ParseFilterExpression(string filterTemplate, ParameterExpression parameter)
        {
            // 解析你的字符串模板并构建表达式树
            // 这里的示例仅支持简单的比较表达式（如 ==、>=、<=）
            // 你可以根据自己的需求进行扩展

            // 示例：解析等于比较
            if (filterTemplate.Contains("=="))
            {
                var parts = filterTemplate.Split(["=="], StringSplitOptions.RemoveEmptyEntries);
                var left = GetPropertyExpression(parts[0], parameter);
                var right = GetValueExpression(parts[1].Trim());
                return Expression.Equal(left, right);
            }

            // 示例：解析大于等于比较
            if (filterTemplate.Contains(">="))
            {
                var parts = filterTemplate.Split([">="], StringSplitOptions.RemoveEmptyEntries);
                var left = GetPropertyExpression(parts[0].Trim(), parameter);
                var right = GetValueExpression(parts[1].Trim().Trim());
                return Expression.GreaterThanOrEqual(left, right);
            }

            // ... 可以继续添加其他的解析逻辑

            throw new ArgumentException("Unsupported filter template.");
        }
        /// <summary>
        /// 获取属性表达式
        /// </summary>
        /// <param name="propertyPath"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static Expression GetPropertyExpression(string propertyPath, Expression parameter)
        {
            // 解析属性路径，构建属性访问表达式
            var properties = propertyPath.Split('.');
            Expression expression = parameter;
            foreach (var property in properties)
            {
                expression = Expression.PropertyOrField(expression, property);
            }
            return expression;
        }
        /// <summary>
        /// 构建访问字典某个键的值的表达式
        /// </summary>
        /// <param name="key"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        static Expression BuildDictionaryAccess(string key, ParameterExpression param)
        {
            // 处理可能的字符串转换
            var dictAccess = Expression.Property(
                param,
                "Item",
                Expression.Constant(key));

            // 转换为字符串比较（简化处理，实际应根据数据类型处理）
            var toString = Expression.Call(dictAccess, "ToString", null, null);
            return toString;
        }
        /// <summary>
        /// 获取值表达式
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ConstantExpression GetValueExpression(string value)
        {
            // 解析值，构建常量表达式
            var valueType = DetermineValueType(value);
            var convertedValue = Convert.ChangeType(value, valueType);
            return Expression.Constant(convertedValue, valueType);
        }
        /// <summary>
        /// 能转换为int类型的就视为int
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Type DetermineValueType(string value)
        {
            // 根据值的类型字符串确定值的实际类型
            // 这里的示例仅支持字符串和整数类型
            // 你可以根据自己的需求进行扩展

            if (int.TryParse(value, out _))
            {
                return typeof(int);
            }
            return typeof(string);
        }

        /// <summary>
        /// 创建将字符串转换为指定类型的lambda表达式
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>转换函数</returns>
        public static Func<string, object?> CreateStringConverter(Type type)
        {
            if (type.Equals(typeof(Byte[])))
            {
                return input => string.IsNullOrWhiteSpace(input) ? null : Convert.FromBase64String(input);
            }
            else if (type.Name.Equals("Boolean"))
            {
                return input => !string.IsNullOrWhiteSpace(input) && !input.Equals("0");
            }
            else
            {
                // 构建这样的lambda表达式result: Func<string, object> result = input => Convert.ChangeType(input, type)
                var param = Expression.Parameter(typeof(string), "input");
                var body = Expression.Convert(Expression.Call(typeof(Convert), "ChangeType", null, param, Expression.Constant(type)), typeof(object));
                var lambda = Expression.Lambda<Func<string, object>>(body, param);
                return lambda.Compile();
            }
        }
    }
}

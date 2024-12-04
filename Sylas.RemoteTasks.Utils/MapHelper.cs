using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Sylas.RemoteTasks.Utils
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

    /// <summary>
    /// 表达式目录树扩展类
    /// </summary>
    public static class ExpressionBuilder
    {
        /// <summary>
        /// 
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
                var parts = filterTemplate.Split(new[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                var left = GetPropertyExpression(parts[0], parameter);
                var right = GetValueExpression(parts[1].Trim());
                return Expression.Equal(left, right);
            }

            // 示例：解析大于等于比较
            if (filterTemplate.Contains(">="))
            {
                var parts = filterTemplate.Split(new[] { ">=" }, StringSplitOptions.RemoveEmptyEntries);
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
        /// 构建一个 将指定对象转换为DynamicParameters 的lambda表达式
        /// </summary>
        /// <param name="parameterFields"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Func<T, DynamicParameters> BuildGetterSqlExecutionParameters<T>(IEnumerable<string> parameterFields)
        {
            Type entityType = typeof(T);

            if (parameterFields is null || !parameterFields.Any())
            {
                parameterFields = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name);
            }

            var type = typeof(DynamicParameters);
            var entity = Expression.Parameter(entityType, "sp");
            var result = Expression.Variable(type, "result");
            var init = Expression.Assign(result, Expression.New(type));
            var addMethod = type.GetMethod("Add", [typeof(string), typeof(object), typeof(DbType?), typeof(ParameterDirection?), typeof(int?)]) ?? throw new Exception($"{type.Name}没有找到Add方法");
            List<Expression> expressions = [init];
            foreach (var fieldName in parameterFields)
            {
                var call = Expression.Call(result,
                    addMethod,
                    Expression.Constant(fieldName),
                    Expression.Convert(Expression.Property(entity, fieldName), typeof(object)),
                    Expression.Default(typeof(DbType?)), Expression.Default(typeof(ParameterDirection?)), Expression.Default(typeof(int?)));
                expressions.Add(call);
            }

            var returnLabel = Expression.Label(type);
            var returnExpression = Expression.Return(returnLabel, result, type);
            var returnLableTarget = Expression.Label(returnLabel, result);

            expressions.Add(returnExpression);
            expressions.Add(returnLableTarget);
            var block = Expression.Block([result], expressions);
            var lambda = Expression.Lambda<Func<T, DynamicParameters>>(block, entity).Compile();
            return lambda;
        }
        /// <summary>
        /// 创建将字符串转换为指定类型的lambda表达式
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>转换函数</returns>
        public static Func<string, object> CreateStringConverter(Type type)
        {
            if (type.Equals("Byte[]"))
            {
                return input => Convert.FromBase64String(input);
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

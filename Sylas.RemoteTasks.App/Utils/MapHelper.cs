using System.Linq.Expressions;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class MapHelper<TIn, TOut>
    {
        /// <summary>
        /// 其结果被赋值给静态只读字段cache。因此，GetFunc()只会在类第一次被访问时被调用一次
        /// </summary>
        private static readonly Func<TIn, TOut> cache = GetFunc();
        private static Func<TIn, TOut> GetFunc()
        {
            ParameterExpression parameterExpression = Expression.Parameter(typeof(TIn), "p");
            List<MemberBinding> memberBindingList = new();

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

            MemberInitExpression memberInitExpression = Expression.MemberInit(Expression.New(typeof(TOut)), memberBindingList.ToArray());
            Expression<Func<TIn, TOut>> lambda = Expression.Lambda<Func<TIn, TOut>>(memberInitExpression, new ParameterExpression[] { parameterExpression });
            return lambda.Compile();
        }
        public static TOut Map(TIn tIn)
        {
            return cache(tIn);
        }
    }

    public static class ExpressionBuilder
    {
        public static IEnumerable<T> ApplyFilter<T>(IEnumerable<T> collection, string filterTemplate)
        {
            var filterExpression = BuildFilterExpression<T>(filterTemplate);
            return collection.Where(filterExpression.Compile());
        }
        public static Expression<Func<T, bool>> BuildFilterExpression<T>(string filterTemplate)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var expression = ParseFilterExpression(filterTemplate, parameter);
            var filterExpression = Expression.Lambda<Func<T, bool>>(expression, parameter);
            return filterExpression;
        }
        public static Expression<Func<T, TResult>> BuildPropertyExpression<T, TResult>(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var propertyExpression = GetPropertyExpression(propertyPath, parameter);
            var lambdaExpression = Expression.Lambda<Func<T, TResult>>(propertyExpression, parameter);
            return lambdaExpression;
        }

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

        public static ConstantExpression GetValueExpression(string value)
        {
            // 解析值，构建常量表达式
            var valueType = DetermineValueType(value);
            var convertedValue = Convert.ChangeType(value, valueType);
            return Expression.Constant(convertedValue, valueType);
        }

        public static Type DetermineValueType(string value)
        {
            // 根据值的类型字符串确定值的实际类型
            // 这里的示例仅支持字符串和整数类型
            // 你可以根据自己的需求进行扩展

            int intValue;
            if (int.TryParse(value, out intValue))
            {
                return typeof(int);
            }
            return typeof(string);
        }
    }
}

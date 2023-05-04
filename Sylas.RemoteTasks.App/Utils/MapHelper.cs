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
                MemberExpression property = Expression.Property(parameterExpression, typeof(TIn).GetProperty(item.Name) ?? throw new Exception($"没有找到{typeof(TIn).Name}的{item.Name}属性"));
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


    // 表达式树构建初始化Person的代码: var p = new Person { Name = "张三", Age = 24 };

    // 创建表达式中的常量部分(Expression.Constant): "张三" 和 24
    // var nameValueExpression = Expression.Constant("张三");
    // var ageValue = Expression.Constant(30);

    // 反射获取属性
    // var nameProperty = typeof(Person).GetProperty(nameof(Person.Name));
    // var ageProperty = typeof(Person).GetProperty(nameof(Person.Age));

    // 创建表达式中的给属性赋值部分(Expression.Bind)
    // var nameBinding = Expression.Bind(nameProperty, nameValueExpression);
    // var ageBinding = Expression.Bind(ageProperty, ageValue);

    // 构建初始化Person对象的表达式(Expression.MemberInit)
    //var newPersonExpr = Expression.MemberInit(
    //            Expression.New(typeof(Person)),
    //            nameBinding,
    //            ageBinding
    //        );

    // 构建返回Person对象的Lambda表达式: () => new Person { Name = "张三", Age = 24 };
    // var personCreator = Expression.Lambda<Func<Person>>(newPersonExpr).Compile();

    // 调用Lambda表达式
    //Person person = personCreator();






    // Bind2
    // 1. 构建namePropertyExpression, 赋值给nameProperty
    //ParameterExpression parameterExpression = Expression.Parameter(typeof(Person), "p");
    //MemberExpression namePropertyExpression = Expression.Property(parameterExpression, nameProperty);
    // 2. 获取要赋值的属性
    //var nameProperty = typeof(Person).GetProperty(nameof(Person.Name))
    // 3. 构建赋值表达式
    //MemberBinding nameBinding = Expression.Bind(nameProperty, namePropertyExpression);
}

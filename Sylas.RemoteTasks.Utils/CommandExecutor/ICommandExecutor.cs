using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.Common.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 执行一些操作的抽象对象
    /// </summary>
    public interface ICommandExecutor
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public IAsyncEnumerable<CommandResult> ExecuteAsync(string command);

        /// <summary>
        /// 根据命令执行器名称创建一个命令执行器
        /// </summary>
        /// <param name="executorName"></param>
        /// <param name="args"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static RequestResult<Func<object[], IAsyncEnumerable<CommandResult>>> Create(string executorName, object[] args, IServiceScopeFactory? serviceScopeFactory = null)
        {
            object? executor = null;
            var t = ReflectionHelper.GetTypeByClassName(executorName);
            if (serviceScopeFactory is not null)
            {
                var exeAttr = t.GetCustomAttribute<ExecutorAttribute>();
                if (exeAttr is not null)
                {
                    executor = exeAttr.GetExecutor(t.Name, serviceScopeFactory);
                }
            }

            if (executor is null)
            {
                bool isStaticClass = t.IsAbstract && t.IsSealed;
                if (!isStaticClass)
                {
                    executor = ReflectionHelper.CreateInstance(t, args);
                }
            }
            
            // 获取 ExecuteAsync 方法
            MethodInfo executeAsyncMethod = t.GetMethods().FirstOrDefault(x => x.Name.Equals("ExecuteAsync"));
            return RequestResult<Func<object[], IAsyncEnumerable<CommandResult>>>.Success(commandHandler);

            // 命令执行器
            async IAsyncEnumerable<CommandResult> commandHandler(object[] parameters)
            {
                var task = executeAsyncMethod.Invoke(executor, parameters);
                if (task is IAsyncEnumerable<CommandResult> enumerableTasks)
                {
                    await foreach (var item in enumerableTasks)
                    {
                        yield return item;
                    }
                    yield break;
                }
                throw new Exception("命令执行者返回的不是正确的命令结果类型");
            }
        }
    }
}

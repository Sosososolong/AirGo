using Microsoft.Extensions.DependencyInjection;
using System;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 自定义标签, 用于指定该类是命令执行器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ExecutorAttribute : Attribute
    {
        /// <summary>
        /// 获取命令执行器
        /// </summary>
        /// <param name="executorName"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <returns></returns>
        public ICommandExecutor? GetExecutor(string executorName, IServiceScopeFactory serviceScopeFactory)
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            ICommandExecutor? executor = scope.ServiceProvider.GetKeyedService<ICommandExecutor>(executorName);
            return executor;
        }
    }
}

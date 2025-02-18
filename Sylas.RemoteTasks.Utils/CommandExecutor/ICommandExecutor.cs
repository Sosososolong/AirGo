using Sylas.RemoteTasks.Common.Dtos;
using System;
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
        public Task<CommandResult> ExecuteAsync(string command);

        /// <summary>
        /// 根据命令执行器名称创建一个命令执行器
        /// </summary>
        /// <param name="executorName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static RequestResult<Func<object[], Task<CommandResult>>> Create(string executorName, object[] args)
        {
            var t = ReflectionHelper.GetTypeByClassName(executorName);
            bool isStaticClass = t.IsAbstract && t.IsSealed;
            object? instance = null;
            if (!isStaticClass)
            {
                instance = ReflectionHelper.CreateInstance(t, args);
            }
            // 获取 ExecuteAsync 方法
            MethodInfo executeAsyncMethod = t.GetMethods().FirstOrDefault(x => x.Name.Equals("ExecuteAsync"));
            // 命令执行器
            async Task<CommandResult> commandHandler(object[] parameters)
            {
                var task = (Task)executeAsyncMethod.Invoke(instance, parameters);
                if (task is Task<CommandResult> commandResultTask)
                {
                    return await commandResultTask;
                }
                throw new Exception("命令执行者返回的不是正确的命令结果类型");
            }
            return RequestResult<Func<object[], Task<CommandResult>>>.Success(commandHandler);
            //try
            //{
            //    if (instance is not ICommandExecutor anythingCommandExecutor)
            //    {
            //        return RequestResult<ICommandExecutor>.Error($"对象无法转换为ICommandExecutor");
            //    }
            //    return RequestResult<ICommandExecutor>.Success(anythingCommandExecutor);
            //}
            //catch (Exception ex)
            //{
            //    return RequestResult<ICommandExecutor>.Error(ex.Message);
            //}
        }
    }
}

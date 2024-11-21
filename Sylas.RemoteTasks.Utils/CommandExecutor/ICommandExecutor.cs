using Sylas.RemoteTasks.Utils.Dto;
using System;
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
        public static RequestResult<ICommandExecutor> Create(string executorName, object[] args)
        {
            var t = ReflectionHelper.GetTypeByClassName(executorName);
            var instance = ReflectionHelper.CreateInstance(t, args);
            try
            {
                if (instance is not ICommandExecutor anythingCommandExecutor)
                {
                    return RequestResult<ICommandExecutor>.Error($"对象无法转换为ICommandExecutor");
                }
                return RequestResult<ICommandExecutor>.Success(anythingCommandExecutor);
            }
            catch (Exception ex)
            {
                return RequestResult<ICommandExecutor>.Error(ex.Message);
            }
        }
    }
}

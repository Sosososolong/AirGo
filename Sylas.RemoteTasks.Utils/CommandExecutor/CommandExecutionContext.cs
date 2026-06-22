using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 命令执行上下文, 包含命令执行所需的环境变量等信息
    /// </summary>
    public class CommandExecutionContext
    {
        /// <summary>
        /// 环境变量
        /// </summary>
        public Dictionary<string, object> EnvironmentVariables { get; set; } = [];
    }
}

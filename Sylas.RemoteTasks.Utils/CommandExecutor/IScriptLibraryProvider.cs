using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 公共脚本库提供者: 为SystemCmd等执行器按语言提供公共脚本片段
    /// 脚本通过语言原生机制引用片段(如Python的import), 片段内容在脚本执行前写入临时目录
    /// </summary>
    public interface IScriptLibraryProvider
    {
        /// <summary>
        /// 获取指定语言的全部公共脚本
        /// </summary>
        /// <param name="lang">语言分区, 如: python/powershell/bash</param>
        /// <returns>相对路径 → 脚本内容, 如: "common/logger.py" → "def log(msg): ..."</returns>
        Task<Dictionary<string, string>> GetScriptsAsync(string lang);
    }
}

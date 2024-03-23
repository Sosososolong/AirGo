using System;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 日志帮助类
    /// </summary>
    public static class LoggerHelper
    {
        /// <summary>
        /// 控制台记录Info级别的日志
        /// </summary>
        /// <param name="info"></param>
        public static void LogInformation(string info)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {info}");
        }
        /// <summary>
        /// 控制台记录错误日志
        /// </summary>
        public static void LogError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {error}");
            Console.ResetColor();
        }

        /// <summary>
        /// 记录重要的日志
        /// </summary>
        /// <param name="critical"></param>
        public static void LogCritical(string critical)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {critical}");
            Console.ResetColor();
        }
    }
}

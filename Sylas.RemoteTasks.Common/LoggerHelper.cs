using System;
using System.IO;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Common
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

        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="logDirectory"></param>
        /// <param name="logFileName"></param>
        /// <returns></returns>
        public static async Task RecordLogAsync(string msg, string logDirectory = "", string logFileName = "")
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Others");
            }
            else if (!Path.IsPathRooted(logDirectory))
            {
                logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", logDirectory);
            }
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            if (string.IsNullOrWhiteSpace(logFileName))
            {
                logFileName = $"{DateTime.Now:yyyy-MM-dd}.log";
            }
            string logFilePath = Path.Combine(logDirectory, logFileName);
            try
            {
                await File.AppendAllTextAsync(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                LogInformation($"心跳日志异常:{ex.Message}");
            }
        }
        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="logDirectory"></param>
        /// <param name="logFileName"></param>
        /// <returns></returns>
        public static void RecordLog(string msg, string logDirectory = "", string logFileName = "")
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Others");
            }
            else if (!Path.IsPathRooted(logDirectory))
            {
                logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", logDirectory);
            }
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            if (string.IsNullOrWhiteSpace(logFileName))
            {
                logFileName = $"{DateTime.Now:yyyy-MM-dd}.log";
            }
            string logFilePath = Path.Combine(logDirectory, logFileName);
            try
            {
                File.AppendAllText(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                LogInformation($"心跳日志异常:{ex.Message}");
            }
        }
    }
}

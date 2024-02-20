using Sylas.RemoteTasks.Utils.CommandExecutor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 系统终端命令助手
    /// </summary>
    public class SystemCmd : ICommandExecutor
    {
        /// <summary>
        /// 无参构造函数
        /// </summary>
        public SystemCmd()
        {
            SolutionName = string.Empty;
            UIProjName = string.Empty;
            CoreProjName = string.Empty;
            InfrastructureProjName = string.Empty;
            SolutionFile = string.Empty;
            UIProjFile = string.Empty;
            CoreProjFile = string.Empty;
            InfrastructureProjFile = string.Empty;
        }
        /// <summary>
        /// 终端命令
        /// </summary>
        /// <param name="projectInfo"></param>
        public SystemCmd(ProjectInfo projectInfo)
        {
            SolutionName = projectInfo.SolutionName;
            UIProjName = projectInfo.UIProjName;
            CoreProjName = projectInfo.CoreProjName;
            InfrastructureProjName = projectInfo.InfrastructureProjName;
            SolutionFile = projectInfo.SolutionFile;
            UIProjFile = projectInfo.UIProjFile;
            CoreProjFile = projectInfo.CoreProjFile;
            InfrastructureProjFile = projectInfo.InfrastructureProjFile;
        }
        /// <summary>
        /// 解决方案名称
        /// </summary>
        public string SolutionName { get; set; }
        /// <summary>
        /// MVC层的名字，"BlogDemo.API"
        /// </summary>
        public string UIProjName { get; set; }
        /// <summary>
        /// Core层名字, BlogDemo.Core
        /// </summary>
        public string CoreProjName { get; set; }
        // Infrastructure层的名字, BlogDemo.Infrastructure
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string SolutionFile { get; }
        /// <summary>
        /// 
        /// </summary>
        public string UIProjFile { get; }
        /// <summary>
        /// 
        /// </summary>
        public string CoreProjFile { get; }
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjFile { get; }
        /// <summary>
        /// "dotnet cli" - 创建项目
        /// </summary>
        public string CreateSlnStatement { get { return "dotnet new sln -o " + SolutionName + "; cd " + SolutionName; } }
        /// <summary>
        /// 
        /// </summary>
        public string CreateWebEmptyStatement { get { return "dotnet new web -o " + UIProjName; } }
        /// <summary>
        /// 
        /// </summary>
        public string CreateLibCoreStatement { get { return "dotnet new classlib -o " + CoreProjName; } }
        /// <summary>
        /// 
        /// </summary>
        public string CreateLibInfrastructureStatement { get { return "dotnet new classlib -o " + InfrastructureProjName; } }
        /// <summary>
        /// "dotnet cli" - 将项目添加到解决方案中
        /// </summary>
        public string AddinSlnAPI => $"dotnet sln {SolutionFile} add ./{UIProjName}/{UIProjFile}";  // dotnet sln BlogDemo.sln add .\BlogDemo.API\BlogDemo.API.csproj
        /// <summary>
        /// 添加解决方案
        /// </summary>
        public string AddinSlnCore => $"dotnet sln {SolutionFile} add ./{CoreProjName}/{CoreProjFile}";
        /// <summary>
        /// 添加解决方案
        /// </summary>
        public string AddinSlnInfrastructure => $"dotnet sln {SolutionFile} add ./{InfrastructureProjName}/{InfrastructureProjFile}";

        /// <summary>
        /// 本地执行脚本
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public async Task<CommandResult> ExecuteAsync(string commands)
        {
            string output = await ExecuteAsync([.. commands.Split(';')], string.Empty);
            return new CommandResult(true, output);
        }

        /// <summary>
        /// 本地执行脚本
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="workingDir"></param>
        /// <returns></returns>
        public async Task<string> ExecuteAsync(List<string> commands, string workingDir)
        {
            // TODO: 修改为配置
            string winShell = "powershell.exe";
            //string winShell = "D:/Program Files/Git/bin/bash.exe";
            var startInfo = new ProcessStartInfo
            {
                // 设置要启动的应用程序
                FileName = Directory.Exists("C:/") ? winShell : "/bin/bash",
                // 是否使用操作系统shell启动
                UseShellExecute = false,
                // 接受来自调用程序的输入信息
                RedirectStandardInput = true,
                // 输出信息
                //RedirectStandardOutput = true,
                // 输出错误
                //RedirectStandardError = true,
                // 不显示程序窗口
                CreateNoWindow = true
            };
            // 设置工作目录
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                startInfo.WorkingDirectory = workingDir;
            }

            Process p = new() { StartInfo = startInfo };
            // 启动程序
            p.Start();

            // 向cmd窗口发送输入信息
            foreach (string cmdItem in commands)
            {
                // 执行一个cmd命令
                await p.StandardInput.WriteLineAsync(cmdItem);
            }

            p.StandardInput.AutoFlush = true;
            p.StandardInput.Close();

            // 获取输出信息
            string successOutput = await p.StandardOutput.ReadToEndAsync();
            string errorOutput = await p.StandardError.ReadToEndAsync();

            // 等待程序退出(等待用户关闭); 局部变量p对象使用了using会在方法执行完毕释放
            // p.WaitForExit();

            return successOutput + Environment.NewLine + Environment.NewLine + errorOutput;
        }
    }
}

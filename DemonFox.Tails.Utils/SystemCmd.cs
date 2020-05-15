﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DemonFox.Tails.Utils
{
    public class SystemCmd
    {
        public SystemCmd(string companyName, string projectName = "Demo", string mvcProjName = "API", string libCoreName = "Core", string libInfrastructureName = "Infrastructure")
        {
            SolutionName = string.IsNullOrEmpty(companyName) ? projectName : companyName + "." + projectName;
            UIProjName = SolutionName + "." + mvcProjName;
            CoreProjName = SolutionName + "." + libCoreName;
            InfrastructureProjName = SolutionName + "." + libInfrastructureName;
        }
        public string SolutionName { get; set; }
        // MVC层的名字，"BlogDemo.API"
        public string UIProjName { get; set; }
        // Core层名字, BlogDemo.Core
        public string CoreProjName { get; set; }
        // Infrastructure层的名字, BlogDemo.Infrastructure
        public string InfrastructureProjName { get; set; }
        public string SolutionFile => SolutionName + ".sln";
        public string UIProjFile => UIProjName + ".csproj";
        public string CoreProjFile => CoreProjName + ".csproj";
        public string InfrastructureProjFile => InfrastructureProjName + ".csproj";

        // "dotnet cli" - 创建项目
        public string CreateSlnStatement { get { return "dotnet new sln -o " + SolutionName + "; cd " + SolutionName; } }
        public string CreateWebEmptyStatement { get { return "dotnet new web -o " + UIProjName; } }
        public string CreateLibCoreStatement { get { return "dotnet new classlib -o " + CoreProjName; } }
        public string CreateLibInfrastructureStatement { get { return "dotnet new classlib -o " + InfrastructureProjName; } }
        // "dotnet cli" - 将项目添加到解决方案中
        public string AddinSlnAPI => $"dotnet sln {SolutionFile} add ./{UIProjName}/{UIProjFile}";  // dotnet sln BlogDemo.sln add .\BlogDemo.API\BlogDemo.API.csproj
        public string AddinSlnCore => $"dotnet sln {SolutionFile} add ./{CoreProjName}/{CoreProjFile}";        
        public string AddinSlnInfrastructure => $"dotnet sln {SolutionFile} add ./{InfrastructureProjName}/{InfrastructureProjFile}";        


        public async Task<string> ExeCmdAsync(List<string> cmdStrs, string workingDir)
        {                                    
            Process p = new Process();
            //设置要启动的应用程序
            p.StartInfo.FileName = "powershell.exe";
            p.StartInfo.WorkingDirectory = workingDir;
            //是否使用操作系统shell启动
            p.StartInfo.UseShellExecute = false;
            // 接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardInput = true;
            //输出信息
            p.StartInfo.RedirectStandardOutput = true;
            // 输出错误
            p.StartInfo.RedirectStandardError = true;
            //不显示程序窗口
            p.StartInfo.CreateNoWindow = true;                        
            //启动程序
            p.Start();

            //向cmd窗口发送输入信息
            foreach (string cmdItem in cmdStrs)
            {
                await p.StandardInput.WriteLineAsync(cmdItem);
            }
            
            // 关闭powershell程序
            await p.StandardInput.WriteLineAsync("exit");

            

            p.StandardInput.AutoFlush = true;
            p.StandardInput.Close();

            // 获取输出信息
            string strOuput = await p.StandardOutput.ReadToEndAsync();

            // 等待程序执行完退出进程
            // 关闭此进程关联的程序(即powershell)，其实上面已经执行了"exit"将powershell程序关闭了
            p.Kill();            
            p.WaitForExit();
            p.Close();
            p.Dispose();

            return strOuput;
        }        
    }
}

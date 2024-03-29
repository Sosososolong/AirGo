﻿using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 描述一个.net项目
    /// </summary>
    /// <param name="baseDir"></param>
    /// <param name="solutionName"></param>
    /// <param name="uiProjName"></param>
    /// <param name="coreProjName"></param>
    /// <param name="infrastructureProjName"></param>
    public class ProjectInfo(string baseDir, string solutionName, string uiProjName, string coreProjName, string infrastructureProjName)
    {
        /// <summary>
        /// 解决方案和项目文件夹
        /// </summary>
        public string SolutionName { get; set; } = solutionName;
        /// <summary>
        /// 
        /// </summary>
        public string UIProjName { get; set; } = uiProjName;
        /// <summary>
        /// 
        /// </summary>
        public string CoreProjName { get; set; } = coreProjName;
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjName { get; set; } = infrastructureProjName;
        // 解决方案和项目名称
        /// <summary>
        /// 
        /// </summary>
        public string SolutionFile => SolutionName + ".sln";
        /// <summary>
        /// 
        /// </summary>
        public string UIProjFile => UIProjName + ".csproj";
        /// <summary>
        /// 
        /// </summary>
        public string CoreProjFile => CoreProjName + ".csproj";
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjFile => InfrastructureProjName + ".csproj";

        /// <summary>
        /// 初始文件夹，包含解决方案文件夹（"F:/工作/T11/BlogDemo.Auto"）
        /// </summary>
        public string BaseDir { get; set; } = baseDir;
        /// <summary>
        /// 解决方案文件夹绝对路径（"F:/工作/T11/BlogDemo.Auto/BlogDemo"）
        /// </summary>
        public string SolutionPath => BaseDir + "/" + SolutionName;
        /// <summary>
        /// 
        /// </summary>
        public string UIProjPath => SolutionPath + "/" + UIProjName;
        /// <summary>
        /// 
        /// </summary>
        public string CoreProjPath => SolutionPath + "/" + CoreProjName;
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjPath => SolutionPath + "/" + InfrastructureProjName;

        private Func<List<string>> SolutionFilesProvider { get; } = () => new List<string>();

        /// <summary>
        /// 解决方案中可能会进行读写等操作的文件
        /// </summary>
        public List<string> SolutionFiles => SolutionFilesProvider();
    }
}

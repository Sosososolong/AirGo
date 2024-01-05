using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils
{
    public class ProjectInfo(string baseDir, string solutionName, string uiProjName, string coreProjName, string infrastructureProjName)
    {
        // 解决方案和项目文件夹
        public string SolutionName { get; set; } = solutionName;
        public string UIProjName { get; set; } = uiProjName;
        public string CoreProjName { get; set; } = coreProjName;
        public string InfrastructureProjName { get; set; } = infrastructureProjName;
        // 解决方案和项目名称
        public string SolutionFile => SolutionName + ".sln";
        public string UIProjFile => UIProjName + ".csproj";
        public string CoreProjFile => CoreProjName + ".csproj";
        public string InfrastructureProjFile => InfrastructureProjName + ".csproj";

        /// <summary>
        /// 初始文件夹，包含解决方案文件夹（"F:/工作/T11/BlogDemo.Auto"）
        /// </summary>
        public string BaseDir { get; set; } = baseDir;
        /// <summary>
        /// 解决方案文件夹绝对路径（"F:/工作/T11/BlogDemo.Auto/BlogDemo"）
        /// </summary>
        public string SolutionPath => BaseDir + "/" + SolutionName;
        public string UIProjPath => SolutionPath + "/" + UIProjName;
        public string CoreProjPath => SolutionPath + "/" + CoreProjName;
        public string InfrastructureProjPath => SolutionPath + "/" + InfrastructureProjName;

        private Func<List<string>> SolutionFilesProvider { get; } = () => new List<string>();

        /// <summary>
        /// 解决方案中可能会进行读写等操作的文件
        /// </summary>
        public List<string> SolutionFiles => SolutionFilesProvider();
    }
}

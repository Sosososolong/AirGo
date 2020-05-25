using System;
using System.Collections.Generic;
using System.Text;

namespace DemonFox.Tails.Core.Entities.Generator
{
    public class ProjectInfo
    {
        public ProjectInfo(string baseDir, string solutionName, string uiProjName, string coreProjName, string infrastructureProjName)
        {
            SolutionName = solutionName;
            UIProjName = uiProjName;
            CoreProjName = coreProjName;
            InfrastructureProjName = infrastructureProjName;
            BaseDir = baseDir;
        }
        // 解决方案和项目文件夹
        public string SolutionName { get; set; }
        public string UIProjName { get; set; }
        public string CoreProjName { get; set; }
        public string InfrastructureProjName { get; set; }
        // 解决方案和项目名称
        public string SolutionFile => SolutionName + ".sln";
        public string UIProjFile => UIProjName + ".csproj";
        public string CoreProjFile => CoreProjName + ".csproj";
        public string InfrastructureProjFile => InfrastructureProjName + ".csproj";

        /// <summary>
        /// 初始文件夹，包含解决方案文件夹（"F:/工作/T11/BlogDemo.Auto"）
        /// </summary>
        public string BaseDir { get; set; }
        /// <summary>
        /// 解决方案文件夹绝对路径（"F:/工作/T11/BlogDemo.Auto/BlogDemo"）
        /// </summary>
        public string SolutionPath => BaseDir + "/" + SolutionName;
        public string UIProjPath => SolutionPath + "/" + UIProjName;
        public string CoreProjPath => SolutionPath + "/" + CoreProjName;
        public string InfrastructureProjPath => SolutionPath + "/" + InfrastructureProjName;

        private Func<List<string>> SolutionFilesProvider { get; }

        /// <summary>
        /// 解决方案中可能会进行读写等操作的文件
        /// </summary>
        public List<string> SolutionFiles => SolutionFilesProvider();
    }
}

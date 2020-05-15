using DemonFox.Tails.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemonFox.Tails.Infrastructure
{
    public class GeneratorContext
    {
        public GeneratorContext(string baseDir, string companyName, string projName)
        {
            BaseDir = baseDir;
            CmdHandler = new SystemCmd(companyName, projName);

            SlnPath = baseDir + "/" + CmdHandler.SolutionName;
            FileHandler = new FileOp(SlnPath);
        }
        public SystemCmd CmdHandler { get; set; }
        public FileOp FileHandler { get; set; }
        /// <summary>
        /// 初始文件夹，包含解决方案文件夹
        /// </summary>
        public string BaseDir { get; set; }
        public List<string> ProjectFiles
        {
            get
            {
                return FileHandler.FindFilesRecursive(SlnPath);
            }
        }
        /// <summary>
        /// 解决方案文件夹（"F:/工作/T11/BlogDemo.Auto/BlogDemo"）
        /// </summary>
        public string SlnPath { get; }

        /// <summary>
        /// 初始化一个项目（包含 API, Core, Infrastructure)
        /// </summary>
        /// <returns></returns>
        public async Task<string> InitialProjectAsync()
        {
            if (!Directory.Exists(BaseDir))
            {
                Directory.CreateDirectory(BaseDir);
            }
            string result = await CmdHandler.ExeCmdAsync(new List<string> {
                CmdHandler.CreateSlnStatement
                ,
                CmdHandler.CreateWebEmptyStatement
                ,
                CmdHandler.CreateLibCoreStatement
                ,
                CmdHandler.CreateLibInfrastructureStatement
                ,
                CmdHandler.AddinSlnAPI
                ,
                CmdHandler.AddinSlnCore
                ,
                CmdHandler.AddinSlnInfrastructure
            }, BaseDir);
            
            FileHandler.DeleteFiles(ProjectFiles.Where(f => f.EndsWith("Class1.cs")).Select(f=>f).ToList());

            return result;
        } 
    }
}

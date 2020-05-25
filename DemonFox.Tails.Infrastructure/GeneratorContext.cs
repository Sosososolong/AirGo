using DemonFox.Tails.Core.Entities.Generator;
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
        public SystemCmd CmdHandler { get; set; }
        public FileOp FileHandler { get; set; }
        public ProjectInfo CurrentProject { get; set; }

        public GeneratorContext(string baseDir, string companyName, string projName = "Demo", string uiProjName = "API", string coreProjName = "Core", string infrastructureProjName = "Infrastructure")
        {                  
            
            string solutionName = string.IsNullOrEmpty(companyName) ? projName : companyName + "." + projName;
            CurrentProject = new ProjectInfo(baseDir, solutionName, uiProjName, coreProjName, infrastructureProjName);
                                             
            CmdHandler = new SystemCmd(CurrentProject);
            FileHandler = new FileOp();
        }
        

        /// <summary>
        /// 初始化一个项目（包含 API, Core, Infrastructure)
        /// </summary>
        /// <returns></returns>
        public async Task<string> InitialProjectAsync()
        {
            if (!Directory.Exists(CurrentProject.BaseDir))
            {
                Directory.CreateDirectory(CurrentProject.BaseDir);
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
            }, CurrentProject.BaseDir);
            
            FileHandler.DeleteFiles(CurrentProject.SolutionFiles.Where(f => f.EndsWith("Class1.cs")).Select(f=>f).ToList());

            return result;
        } 
    }
}

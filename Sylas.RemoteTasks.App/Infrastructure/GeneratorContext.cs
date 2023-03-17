using System.Text.RegularExpressions;
using System.Text;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Entities;

namespace Sylas.RemoteTasks.App.Infrastructure
{
    public class GeneratorContext
    {
        public SystemCmd CmdHandler { get; set; }
        public ProjectInfo CurrentProject { get; set; }

        public GeneratorContext(string baseDir, string companyName, string projName = "Demo", string uiProjName = "API", string coreProjName = "Core", string infrastructureProjName = "Infrastructure")
        {

            string solutionName = string.IsNullOrEmpty(companyName) ? projName : companyName + "." + projName;
            CurrentProject = new ProjectInfo(baseDir, solutionName, uiProjName, coreProjName, infrastructureProjName);

            CmdHandler = new SystemCmd(CurrentProject);
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
            string result = await CmdHandler.ExecuteAsync(new List<string> {
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

            FileHelper.DeleteFiles(CurrentProject.SolutionFiles.Where(f => f.EndsWith("Class1.cs")).Select(f => f).ToList());

            return result;
        }

        /// <summary>
        /// 获取解决方案内的AspNetCore Web程序的根目录
        /// </summary>
        /// <param name="dir">解决方案文件夹</param>
        /// <returns></returns>
        public List<string> GetAspNetCoreWebDirectory(string dir)
        {
            // 当前目录下面有Startup.cs文件和Program.cs文件, 说明此文件夹很可能是asp.net core Web程序
            List<string> webAppDirsMaybe = FileHelper.GetDirectorysRecursive(FileHelper.GetSolutionDirectory(),
                d => Directory.GetFiles(d).AsQueryable().Where(f => f.EndsWith(@"\Startup.cs")).Count() > 0  // 如果当前目录下有Startup.cs文件则符合条件
                    && Directory.GetFiles(d).AsQueryable().Where(f => f.EndsWith(@"\Program.cs")).Count() > 0  // 如果当前目录下有Program.cs文件则符合条件
                );
            if (webAppDirsMaybe.Count == 0)
            {
                throw new Exception("无法找到web项目");
            }

            List<string> webAppDirs = new List<string>();
            foreach (var dirItem in webAppDirsMaybe)
            {
                // 在目录dirItem下找到"\Controllers"文件夹
                List<string> controllerDir = FileHelper.GetDirectorysRecursive(dirItem, d => d.EndsWith(@"\Controllers") && Directory.GetFiles(d).AsQueryable().Where(f => f.EndsWith("Controller.cs")).Count() > 0);
                // 找到了说明dirItem是AspNetCore Web项目
                if (controllerDir.Count > 0)
                {
                    webAppDirs.Add(dirItem);
                }
            }

            return webAppDirs;
        }

        /// <summary>
        /// 添加代码: Dto排序
        /// </summary>
        /// <param name="destinationUIDir">AspNetCore Web程序根目录</param>
        /// <param name="dtoName">Dto类名</param>
        /// <param name="entityName">对应的实体类名</param>
        /// <returns></returns>
        public OperationResult AddDtoToEntityMapForOrderBy(string propertyMappingServiceFile, string dtoName, string entityName, string dtoProjectDir, string entityProjectDir)
        {
            // 找到文件
            // string propertyMappingServiceFile = FileHelper.GetFilesRecursive(destinationUIDir, f => f.EndsWith(@"\PropertyMappingService.cs"), ls => ls.Count > 0).FirstOrDefault();            
            if (!File.Exists(propertyMappingServiceFile))
            {
                throw new ArgumentNullException(nameof(propertyMappingServiceFile));
            }
            //FileHelper.InsertContent(propertyMappingServiceFile, originCode =>
            //{
            //    return GetNewCode(originCode,
            //        @"\r\n(\s+)modelsPropertyMappingList\.Add\(new ModelsPropertyMapping<.+,.+>\(.+\)\);\r\n(\s+)}",
            //        $"modelsPropertyMappingList.Add(new ModelsPropertyMapping<{dtoName}, {entityName}>(_{entityName.ToLower()}PropertyMapping));",
            //        "}");
            //});

            string mappingItems = GetDtoToEntityPropertiesMappingDicItemsStatement(dtoName, entityName, dtoProjectDir, entityProjectDir);
            if (mappingItems.StartsWith("error"))
            {
                return new OperationResult(false, "插入代码失败: " + mappingItems.Split(':')[1]);
            }
            string codeStart = $"modelsPropertyMappingList.Add(new ModelsPropertyMapping<{dtoName}, {entityName}>(";
            if (FileHelper.IsContentExists(propertyMappingServiceFile, codeStart))
            {
                return new OperationResult(false, "映射关系似乎已经添加过了, 如下: \r\n" + codeStart + "...");
            }
            FileHelper.InsertContent(propertyMappingServiceFile, originCode =>
            {
                return GetNewCode(originCode,
                    @"\r\n(\s+){\s*"".+"",\s*new PropertyMappingValue\(.+\).+\r\n\s+}\)\);\r\n(\s+)}",
                    $@"modelsPropertyMappingList.Add(new ModelsPropertyMapping<{dtoName}, {entityName}>(new Dictionary<string, PropertyMappingValue>(StringComparer.OrdinalIgnoreCase)
            {{
                {mappingItems}
            }}));",
                    "}");
            });

            return new OperationResult(true, "");
        }
        /// <summary>
        /// 为当前dto封装排序参数
        /// </summary>
        /// <param name="dtoParametersFile"></param>
        /// <returns></returns>
        public OperationResult AddNewProp(string dtoParametersFile, string propTypeString, string propName, string defaultOrderByField = "")
        {
            if (!File.Exists(dtoParametersFile))
            {
                return new OperationResult(false, "文件不存在");
            }
            List<string> dtoProps = FileHelper.GetProperties(dtoParametersFile);
            if (dtoProps.Contains(propName))
            {
                return new OperationResult(false, $"添加属性{propName}失败, 属性{propName}已经存在");
            }
            string newPropStatement = string.IsNullOrWhiteSpace(defaultOrderByField) ? $"public {propTypeString} {propName} {{ get; set; }}" : $@"public string OrderBy {{ get; set; }} = ""{defaultOrderByField}"";";
            FileHelper.InsertContent(dtoParametersFile, originCode =>
            {
                return GetNewCodeProperty(originCode,
                    @"\r\n\s+}\r\n}",
                    newPropStatement);
            });
            return new OperationResult(true, "");
        }

        /// <summary>
        /// 将服务添加到依赖注入容器
        /// </summary>
        /// <typeparam name="TIService"></typeparam>
        /// <typeparam name="IService"></typeparam>
        /// <param name="startupFile"></param>
        /// <returns></returns>
        public OperationResult AddServiceToDIContainer(string startupFile, string iserviceName, string serviceName)
        {
            if (!File.Exists(startupFile))
            {
                return new OperationResult(false, $"配置文件文件{startupFile}不存在");
            }
            string codeInserting = $"services.AddTransient<{iserviceName}, {serviceName}>();";
            if (FileHelper.IsContentExists(startupFile, codeInserting))
            {
                return new OperationResult(false, $"在Startup文件中发现了代码{codeInserting}, 说明服务{iserviceName}已经注册过了");
            }
            FileHelper.InsertContent(startupFile, originCode =>
            {
                return GetNewCode(originCode,
                    @"\r\n(\s+)services\..+;\r\n(\s+)}",
                    codeInserting,
                    "}");
            });
            return new OperationResult(true, "");
        }
        /// <summary>
        /// 在某个类文件的某个方法的最后添加代码
        /// </summary>
        /// <param name="startupFile">Startup文件</param>
        /// <param name="addServiceCodes">添加服务的代码</param>
        /// <returns></returns>
        public OperationResult AddCodesAtMethodEnd(string file, string matchPattern, string addServiceCodes)
        {
            if (!File.Exists(file))
            {
                return new OperationResult(false, $"文件{file}不存在");
            }

            FileHelper.InsertContent(file, originCode =>
            {
                Regex regex = new Regex(matchPattern);
                var match = regex.Match(originCode);
                string methodCon = match.Value;
                string methodTail = "\r\n        }";
                string removedMethodCon = methodCon.Remove(methodCon.LastIndexOf("\r\n"));
                string codes = removedMethodCon + $"\r\n            {addServiceCodes}" + methodTail;
                return regex.Replace(originCode, codes);
            });
            return new OperationResult(true, "");
        }
        //public OperationResult AddCustomerDbContextServiceToDIContainer(string startupFile)
        /// <summary>
        /// 在系统入口方法Main()方法中添加重置数据库的代码, 即删除数据库, 执行迁移(update-database)
        /// </summary>
        /// <param name="programFile"></param>
        /// <returns></returns>
        public OperationResult AddDbResetCode(string programFile)
        {
            if (!File.Exists(programFile))
            {
                return new OperationResult(false, $"没有找到文件{programFile}");
            }
            string codeInserting = $@"// db reset start
            using (var scope = host.Services.CreateScope())
            {{
                try
                {{                                 
                    var dbContext = scope.ServiceProvider.GetService<MyDbContext>();
                    dbContext.Database.EnsureDeleted();
                    dbContext.Database.Migrate();
                }}
                catch (Exception e)
                {{
                    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
                    logger.LogError(e, ""Database Migration Error"");
                    throw;
                }}
            }}
            // db reset end
";
            if (FileHelper.IsContentExists(programFile, "dbContext.Database.Migrate();"))
            {
                return new OperationResult(false, $"在Program.cs中已经发现了类似数据库迁移的代码");
            }
            FileHelper.InsertContent(programFile, originCode =>
            {
                return GetNewCode(originCode,
                    @"\r\n(\s*)var host = CreateHostBuilder\(args\)\.Build\(\);\r\n(\s)",
                    codeInserting,
                    "");
            });
            return new OperationResult(true, "");
        }


        /// <summary>
        /// 原始代码根据需求更新为满足需求的代码
        /// </summary>
        /// <param name="originCode"></param>
        /// <param name="regexPattern">正则字符串, 匹配要插入代码的位置前1行(包括前2行的换行符)和后一行代码</param>
        /// <param name="codeInserting">要插入的新代码</param>
        /// <param name="codeNextInserting">插入新代码位置的下一行代码</param>
        /// <returns></returns>
        public string GetNewCode(string originCode, string regexPattern, string codeInserting, string codeAfterInserting)
        {
            Regex reg = new Regex(regexPattern); // 
            Match match = reg.Match(originCode);
            if (match.Groups.Count != 3)
            {
                throw new Exception("正则匹配结果似乎不正确");
            }
            string originMatchedCon = match.Groups[0].Value;
            //string spacesFirst = match.Groups[1].Value; // 匹配的代码的第一行缩进的空格
            string spacesLast = match.Groups[2].Value; // 匹配的代码的最后一行缩进的空格
            string newCode = originMatchedCon.Remove(originMatchedCon.LastIndexOf("\r\n")) + "\r\n" +
                "            " + codeInserting + "\r\n" +  // 插入的新代码
                spacesLast + codeAfterInserting;
            return reg.Replace(originCode, newCode);
        }

        /// <summary>
        /// 插入一条属性
        /// </summary>
        /// <param name="originCode"></param>
        /// <param name="regexPattern"></param>
        /// <param name="propStatement"></param>
        /// <returns></returns>
        public string GetNewCodeProperty(string originCode, string regexPattern, string propStatement)
        {
            Regex reg = new Regex(regexPattern);
            Match match = reg.Match(originCode);
            string originMatchedCon = match.Groups[0].Value;
            string newCode = Environment.NewLine + "        " + propStatement + originMatchedCon;
            return reg.Replace(originCode, newCode);
        }

        /// <summary>
        /// 排序用, 通过dto和entity名称获取他们属性对应的映射关系的字典的所有键值对代码
        /// </summary>
        /// <param name="dtoName">dto类名, 如:EmployeeDto</param>
        /// <param name="entityName">entity类名, 如:Employee</param>
        /// <param name="dirOfFile">dto</param>
        /// <returns></returns>
        public string GetDtoToEntityPropertiesMappingDicItemsStatement(string dtoName, string entityName, string dtoProjectDir, string entityProjectDir)
        {
            string dto = dtoName;
            string entity = entityName;

            string dtoFile = FileHelper.FindFilesRecursive(dtoProjectDir, f => f.EndsWith($"{dto}.cs"), ls => ls.Count > 0).FirstOrDefault();
            string entityFile = FileHelper.FindFilesRecursive(entityProjectDir, f => f.EndsWith($"{entity}.cs"), ls => ls.Count > 0).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dtoFile))
            {
                return $"error:没有在文件夹{dtoProjectDir}中找到文件{dto}.cs";
            }
            if (string.IsNullOrWhiteSpace(entityFile))
            {
                return $"error:没有在文件夹{entityProjectDir}中找到文件{entity}.cs";
            }

            List<string> dtoProperties = FileHelper.GetProperties(dtoFile);
            List<string> entityProperties = FileHelper.GetProperties(entityFile);
            StringBuilder codeSb = new StringBuilder();
            bool f = false;
            foreach (string dtoProp in dtoProperties)
            {
                // 从第二条语句开始添加缩进
                if (f)
                {
                    codeSb.Append("                ");
                }
                f = true;
                if (entityProperties.Contains(dtoProp))
                {
                    codeSb.Append($"{{ \"{dtoProp}\", new PropertyMappingValue(new List<string> {{ \"{dtoProp}\" }}) }},\r\n");
                }
                else
                {
                    // dto的当前属性在entity中找不到同名属性, 需要用户手动指定对应entity的属性
                    Console.WriteLine($"{dto}.{dtoProp} 对应 {entity}类的哪些属性, 用逗号隔开, entity的所有属性");
                    foreach (var item in entityProperties)
                    {
                        Console.Write(item + "     ");
                    }
                    Console.Write(Environment.NewLine);

                    string propertyMappingsValueStr = string.Empty;
                    while (true)
                    {
                        propertyMappingsValueStr = Console.ReadLine();
                        string[] inputs = propertyMappingsValueStr.Split(',');
                        bool isInputAllRight = true;
                        foreach (var item in inputs)
                        {
                            if (!entityProperties.Contains(item))
                            {
                                isInputAllRight = false;
                                Console.WriteLine("输入的属性似乎不完全正确, 请重新输入属性, 或者修改源代码");
                                break;
                            }
                        }
                        if (isInputAllRight)
                        {
                            break;
                        }
                    }
                    Console.WriteLine("排序升降是否需要反转? y/n");
                    string isRevert = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(isRevert))
                    {
                        isRevert = "n";
                    }
                    string[] propertyMappingsValueStrArr = propertyMappingsValueStr.Trim().Split(',');
                    codeSb.Append($@"{{ ""{dtoProp}"", new PropertyMappingValue(new List<string> {{ ");

                    for (int i = 0; i < propertyMappingsValueStrArr.Length; i++)
                    {
                        if (i == propertyMappingsValueStrArr.Length - 1)
                        {
                            codeSb.Append($@"""{propertyMappingsValueStrArr[i]}""");
                        }
                        else
                        {
                            codeSb.Append($@"""{propertyMappingsValueStrArr[i]}"", ");
                        }
                    }

                    codeSb.Append(" }");
                    codeSb.Append(isRevert.Trim().ToLower() == "y" ? ", true" : "");
                    codeSb.Append(") },\r\n");
                }
            }
            string res = codeSb.ToString().TrimEnd(',', '\r', '\n');
            return res;
        }

        #region 为一个Entity添加Api
        public OperationResult ValidateMyDbContext(string dbContextDir)
        {
            List<string> dbContextFiles = FileHelper.FindFilesRecursive(dbContextDir, f => f.EndsWith("DbContext.cs"));
            if (dbContextFiles.Count > 0)
            {
                return new OperationResult(true, "") { Data = dbContextFiles };
            }
            return new OperationResult(false, "没有找到自定义的DbContext文件, 或者文件后缀不是DbContext");
        }
        #endregion
    }
}

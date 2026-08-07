using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RazorEngine.Templating;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Constants;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor;
/// <summary>
/// 文件操作帮助类
/// </summary>
[Executor]
public class FileHelper(AiService aiService, CommandExecutionContext executionContext) : ICommandExecutor
{
    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="files"></param>
    public static void DeleteFiles(List<string> files)
    {
        foreach (string file in files)
        {
            File.Delete(file);
        }
    }
    /// <summary>
    /// 递归查找文件
    /// </summary>
    /// <param name="dirPath"></param>
    /// <returns></returns>
    public static List<string> FindFilesRecursive(string dirPath)
    {
        List<string> files = [];
        string[] findedFiles = Directory.GetFiles(dirPath).Select(x => x.Replace('\\', '/')).ToArray();
        files.AddRange(findedFiles);

        string[] dirs = Directory.GetDirectories(dirPath);
        if (!dirs.Any())
        {
            return files;
        }
        foreach (string dir in dirs)
        {
            if (dir.EndsWith(@"\obj") || dir.EndsWith(@"\bin") || dir.EndsWith(@"\.vs"))
            //if (excludes.Any(dir.Contains))
            {
                continue;
            }
            files.AddRange(FindFilesRecursive(dir));
        }
        return files;
    }
    /// <summary>
    /// 查找源文件
    /// </summary>
    /// <param name="slnDir"></param>
    /// <param name="sourceFileType"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static List<string> FindSourceFiles(string slnDir, SourceFileType sourceFileType)
    {
        var sourceFiles = FindFilesRecursive(slnDir);
        if (sourceFileType == SourceFileType.Appsettings)
        {
            var appsettings = sourceFiles.Where(x => x.Contains("appsettings") && x.Contains(".json")).ToList();
            return appsettings;
        }
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取当前结局方案文件夹
    /// </summary>
    /// <returns></returns>
    public static string GetSolutionDirectory()
    {
        // "D:\asp.net\Routine\Routine.Test\bin\Debug\netcoreapp3.1"
        string exeDir = Directory.GetCurrentDirectory();
        // 当前控制台程序根目录: "D:\asp.net\Routine\Routine.Test"
        string currentProjectDir = exeDir.Remove(exeDir.IndexOf(@"\bin\"));
        string solutionDir = currentProjectDir.Remove(currentProjectDir.LastIndexOf(@"\"));
        return solutionDir;
    }

    /// <summary>
    /// 展示解决方案目录下的所有文件夹
    /// </summary>
    public static string[] GetDirectoriesUnderSolution()
    {
        string solutionDir = GetSolutionDirectory();
        return Directory.GetDirectories(solutionDir);
    }

    /// <summary>
    /// 递归获取一个目录下的所有文件
    /// </summary>
    /// <param name="dir">要查找的目录</param>
    /// <param name="filter">需要查找的文件的过滤条件</param>
    /// <param name="files">存放文件路径的容器, 递归调用时候传值用的</param>     
    /// <param name="stopRecursionConditionOfFiles">files满足一定的条件就停止递归搜索</param>     
    /// <returns></returns>
    public static List<string> FindFilesRecursive(string dir, Expression<Func<string, bool>>? filter = null, Func<List<string>, bool>? stopRecursionConditionOfFiles = null, List<string>? files = null)
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new ArgumentNullException(nameof(dir));
        }
        if (!Directory.Exists(dir)) // 目录都不存在, 那么文件肯定不存在, 返回空集合即可
        {
            return [];
        }
        filter ??= s => true;
        files ??= [];
        // 当前目录下的所有目录
        string[] currentDirs = Directory.GetDirectories(dir);
        string[] currentFiles = Directory.GetFiles(dir);
        files.AddRange(currentFiles.AsQueryable().Where(filter).Select(x => x.Replace('\\', '/')));
        if (!currentDirs.Any())
        {
            return files;
        }
        // 当files集合满足一定条件就停止递归
        if (stopRecursionConditionOfFiles != null && stopRecursionConditionOfFiles(files))
        {
            return files;
        }
        foreach (string dirItem in currentDirs)
        {
            if (dirItem.EndsWith(@"\obj") || dirItem.EndsWith(@"\bin") || dirItem.EndsWith(@"\.vs"))
            {
                continue;
            }
            FindFilesRecursive(dirItem, filter, stopRecursionConditionOfFiles, files);
        }
        return files;
    }
    /// <summary>
    /// 递归获取所有某个目录下的所有目录
    /// </summary>
    /// <param name="dir">操作目录</param>
    /// <param name="filter">目录的过滤条件</param>
    /// <param name="dirs">存放目录的容器, 递归调用时候传值用的</param>
    /// <param name="stopRecursionConditionOfDirs">dirs满足一定的条件则停止递归, 比如元素个数大于0的时候</param>
    /// <returns></returns>
    public static List<string> GetDirectoriesRecursive(string dir,
        Expression<Func<string, bool>>? filter = null,
        Func<List<string>, bool>? stopRecursionConditionOfDirs = null,
        List<string>? dirs = null
        )
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new ArgumentNullException(nameof(dir));
        }
        if (!Directory.Exists(dir))
        {
            throw new ArgumentException($"目录{dir}不存在");
        }
        filter ??= s => true;
        dirs ??= [];
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new ArgumentNullException(nameof(dir));
        }

        // 当前目录下的所有符合条件的目录
        string[] currentDirs = Directory.GetDirectories(dir);
        dirs.AddRange(currentDirs.AsQueryable().Where(filter).ToList());
        // 如果目录下面已经没有目录了停止递归; 如果条件存在, 那么当前已经获取的dirs满足条件则停止递归查询
        if (!currentDirs.Any() || stopRecursionConditionOfDirs != null && stopRecursionConditionOfDirs(dirs))
        {
            return dirs;
        }
        foreach (string dirItem in currentDirs)
        {
            if (dirItem.EndsWith(@"\obj") || dirItem.EndsWith(@"\bin") || dirItem.EndsWith(@"\.vs"))
            {
                continue;
            }
            GetDirectoriesRecursive(dirItem, filter, stopRecursionConditionOfDirs, dirs);
        }

        return dirs;
    }

    /// <summary>
    /// 在一个文件中插入代码片段, 思路是把原始文本经过替换部分字符串的方式得到的新代码重新覆盖写入文件
    /// </summary>
    /// <param name="file">要修改的文件</param>
    /// <param name="newContentHandler">通过原始文本得到新文本(替换原始文本中的部分字符串)的逻辑</param>
    public static void InsertContent(string file, Func<string, string> newContentHandler)
    {
        string originContent = string.Empty;
        using (StreamReader sr = new(file, new UTF8Encoding(false)))
        {
            sr.BaseStream.Seek(0, SeekOrigin.Begin);
            originContent = sr.ReadToEnd();
            sr.Close();
        }

        string newCon = newContentHandler(originContent);

        using var streamWriter = new StreamWriter(file, false, new UTF8Encoding(false));
        streamWriter.Write(newCon);
        streamWriter.Flush();
        streamWriter.Close();
    }

    /// <summary>
    /// 将格式化过的json字符串转换为紧凑的一般字符串
    /// </summary>
    /// <param name="settings">经过格式化的json字符串</param>
    /// <returns></returns>
    public static string UnformatJsonString(string settings)
    {
        // 去掉json字符串的换行符, 大括号后面的空格, 冒号后面的空格
        string json = settings.Replace("\r\n", "").Replace("{ ", "{").Replace(": ", ":");
        // 去掉","后面的空格/缩进
        Regex regex = new(@",\s+");
        json = regex.Replace(json, ",");
        // 去掉"{"后面的空格/缩进
        regex = new(@"{\s+");
        json = regex.Replace(json, "{");
        // 去掉"}"前面的空格/缩进
        regex = new(@"\s+}");
        return regex.Replace(json, "}");
    }

    /// <summary>
    /// 获取json文件的内容并反序列化为对象
    /// </summary>
    /// <param name="settingsFile">json配置文件</param>
    /// <param name="settingsHandler">反序列化前对配置文件的内容进行预处理</param>
    /// <returns></returns>
    public static dynamic? SettingsRead(string settingsFile, Func<string, string>? settingsHandler = null)
    {
        if (!File.Exists(settingsFile))
        {
            throw new ArgumentException($"配置文件{settingsFile}不存在");
        }
        dynamic? settingsObj = null;
        using (StreamReader sr = new(settingsFile, new UTF8Encoding(false)))
        {
            string settings = sr.ReadToEnd();
            // 将配置文件内容反序列化为对象
            settingsObj = JsonConvert.DeserializeObject(settings);
        }
        return settingsObj;
    }

    /// <summary>
    /// 通过一个类文件, 获取该类的所有属性字符串
    /// </summary>
    /// <param name="classFile"></param>
    /// <returns></returns>
    public static List<string> GetProperties(string classFile)
    {
        List<string> properties = [];
        using (StreamReader sr = new(classFile, new UTF8Encoding(false)))
        {
            string fileCon = sr.ReadToEnd();
            var reg = new Regex(@"public\s+.+\s+(\w+)\s*{\s*get;\s*set;\s*}");
            MatchCollection matches = reg.Matches(fileCon);

            foreach (Match item in matches.Cast<Match>())
            {
                properties.Add(item.Groups[1].Value);
            }
            sr.Close();
        }
        return properties;
    }

    /// <summary>
    /// 将内容写入文件
    /// </summary>
    /// <param name="file"></param>
    /// <param name="input"></param>
    /// <param name="append"></param>
    public static async Task WriteAsync(string file, string input, bool append)
    {
        using StreamWriter sw = new(file, append, new UTF8Encoding(false));
        await sw.WriteAsync(input);
        sw.Flush();
        sw.Close();
    }

    /// <summary>
    /// 判断一个文件中是否已经存在指定内容
    /// </summary>
    /// <param name="file"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public static bool IsContentExists(string file, string content)
    {
        StreamReader streamReader = new(file, new UTF8Encoding(false));
        using StreamReader sr = streamReader;
        string fileContent = sr.ReadToEnd();
        sr.Close();
        return fileContent.Contains(content);
    }

    /// <summary>
    /// 获取文件的编码格式
    /// </summary>
    /// <param name="filename">文件名</param>
    /// <returns>编码格式(UTF8、BigEndianUnicode、Unicode、Default Ansi)</returns>
    public static Encoding GetFileEncoding(string filename)
    {
        Encoding encoding = Encoding.Default;
        var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
        if (fs.Length <= 0)
        {
            throw new Exception("file length is zero, file encoding cannot be got...");
        }

        var br = new BinaryReader(fs);
        byte[] buffer = br.ReadBytes(2);
        if (buffer[0] >= 0xEF)
        {
            if (buffer[0] == 0xEF && buffer[1] == 0xBB)
            {
                encoding = Encoding.UTF8;
            }
            else if (buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode;
            }
            else encoding = buffer[0] == 0xFF && buffer[1] == 0xFE ? Encoding.Unicode : Encoding.Default;
        }
        br.Close();
        br.Dispose();
        fs.Close();
        fs.Dispose();
        return encoding;
    }
    /// <summary>
    /// 给类文件追加属性代码
    /// </summary>
    /// <param name="file"></param>
    /// <param name="code"></param>
    public static void InsertCodeProperty(string file, string code)
    {
        InsertContent(file, originCode =>
        {
            if (originCode.Contains(code))
            {
                throw new Exception("要插入的代码已经存在");
            }
            // 找到最后一个属性
            Regex regex = new Regex(@"public\s+.+\s+\w+\s*{\s*get;\s*set;\s*}");
            Match lastProperty = regex.Matches(originCode).LastOrDefault();
            if (lastProperty != null)
            {
                string lastPropertyString = lastProperty.Value; // "public DbSet<Employee> Employees { get; set; }"
                string modifiedPropertyString = lastPropertyString + Environment.NewLine + SpaceConstants.TwoTabsSpaces + code;
                return originCode.Replace(lastPropertyString, modifiedPropertyString); // 匹配的属性应该是独一无二的, 所以这里肯定只是替换一次
            }
            // 添加第一个属性
            regex = new Regex(@"\s{8}.+\(");
            // 找到文件中第一个方法的声明语句
            string firstMethodStatement = regex.Match(originCode).Value;
            if (string.IsNullOrEmpty(firstMethodStatement))
            {
                regex = new Regex(@"\s{4}{");

                string classLeftBrackets = regex.Match(originCode).Value;
                classLeftBrackets = classLeftBrackets + Environment.NewLine + SpaceConstants.TwoTabsSpaces + code;

                return regex.Replace(originCode, classLeftBrackets, 1); // 只替换一次
            }
            string newCodeBeforeFirstMethodStatement = code + firstMethodStatement;
            return originCode.Replace(firstMethodStatement, newCodeBeforeFirstMethodStatement);
        });

    }
    /// <summary>
    /// 向一个方法中插入代码段字符串
    /// </summary>
    /// <param name="classFile">方法所在的类文件</param>
    /// <param name="methodName">方法名</param>
    /// <param name="codes">要插入的代码</param>
    /// <param name="insertPositionPredicate">怎么找到插入代码的位置(插到找到的位置后面)</param>
    /// <param name="codesExistPredicate">如果只插入一句代码, 怎么判断要插入的代码是否存在, 如果插入的是代码段的话暂时不传参数</param>
    public static void InsertCode(string classFile, string methodName, string codes, Func<StatementSyntax, bool>? insertPositionPredicate = null, Func<StatementSyntax, bool>? codesExistPredicate = null)
    {
        InsertContent(classFile, originCode =>
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(originCode);
            SyntaxNode root = tree.GetRoot();
            MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == methodName).First();
            if (method.Body is null)
            {
                return string.Empty;
            }
            SyntaxList<StatementSyntax> methodStatements = method.Body.Statements;
            // 如果方法中没有代码
            if (methodStatements.FirstOrDefault() == null)
            {
                Regex regex = new(methodName + @"\(.*\r\n\s{8}{[\w\W]*?\s{8}}");
                Match onModelCreatingMatch = regex.Match(originCode);
                string oldPart = onModelCreatingMatch.Value;
                string newPart = onModelCreatingMatch.Value.TrimEnd('}') + SpaceConstants.OneTabSpaces + codes + Environment.NewLine + SpaceConstants.TwoTabsSpaces + "}";
                return originCode.Replace(oldPart, newPart);
            }
            if (codesExistPredicate is null)
            {
                if (method.Body.ToString().Contains(codes))
                {
                    throw new Exception("要插入的代码已经存在");
                }
            }
            // 如果要插入的语句已经存在
            else if (methodStatements.Where(codesExistPredicate).FirstOrDefault() != null)
            {
                throw new Exception("要插入的代码已经存在");
            }


            StatementSyntax? insertPositionStatement = insertPositionPredicate != null
                                        ? methodStatements.Where(insertPositionPredicate).LastOrDefault()
                                        : methodStatements.LastOrDefault();
            insertPositionStatement ??= methodStatements.LastOrDefault() ?? throw new Exception("无法创建插入点表达式");

            string insertPositionTrivia = insertPositionStatement.GetTrailingTrivia().ToString();
            if (string.IsNullOrEmpty(insertPositionTrivia))
            {
                insertPositionTrivia = Environment.NewLine;
            }

            string insertPosition = insertPositionStatement.ToString();
            string newCodes = insertPosition.ToString() + insertPositionTrivia + Environment.NewLine + SpaceConstants.OneTabSpaces + SpaceConstants.TwoTabsSpaces + codes;
            return originCode.Replace(insertPosition, newCodes);
        });
    }

    /// <summary>
    /// 从json文件中获取所有数据
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="recordsFields">["result","data"]表示 response["result"]["data"]</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static JArray GetRecords(string filePath, string[] recordsFields)
    {
        // { "Records: [ {..}, {..}, ... ] }
        string jsonContent = File.ReadAllText(filePath);
        var jsonObj = JsonConvert.DeserializeObject<JToken>(jsonContent);
        JArray records;
        if (!recordsFields.Any())
        {
            records = jsonObj as JArray ?? [];
        }
        else
        {
            foreach (var jsonField in recordsFields)
            {
                jsonObj = (jsonObj as JObject)?[jsonField];
                if (jsonObj is null)
                {
                    throw new Exception($"字段{jsonField}为空");
                }
            }

            // jsonObj已经指向数据"Records"
            records = jsonObj as JArray ?? throw new Exception($"{string.Join(',', recordsFields)}字段不是数据数组");
        }
        return records;
    }
    /// <summary>
    /// 分组正则搜索文件, 搜索结果的每一项的分组名作为属性, 分组搜索结果为属性值构建对象集合
    /// </summary>
    /// <param name="filePath">要搜索的文件</param>
    /// <param name="targetPattern">含分组的正则表达式</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<List<Dictionary<string, string>>> SearchTxtAsync(string filePath, string targetPattern)
    {
        var groupNamesPattern = RegexConst.PatternGroup.Matches(targetPattern);
        var groupNames = new List<string>();
        foreach (Match groupNameMatch in groupNamesPattern.Cast<Match>())
        {
            var groupName = groupNameMatch.Groups[1].Value;
            groupNames.Add(groupName);
        }

        // 30368
        var searchedResult = new List<Dictionary<string, string>>();

        string con = await File.ReadAllTextAsync(filePath);
        var matches = Regex.Matches(con, targetPattern);
        int i = 0;
        var start = DateTime.Now;
        foreach (Match item in matches.Cast<Match>())
        {
            i++;
            Dictionary<string, string> current = [];

            foreach (var groupName in groupNames)
            {
                // TODO: TryGetValue ? 失败continue
                var groupValue = item.Groups[groupName].Value;
                current[groupName] = groupValue;
            }

            var hasExist = false;
            foreach (var result in searchedResult)
            {
                var allFieldsEquals = true;
                foreach (var groupName in groupNames)
                {
                    if (result[groupName].ToString() != current[groupName].ToString())
                    {
                        allFieldsEquals = false;
                        break;
                    }
                }
                if (allFieldsEquals)
                {
                    hasExist = true;
                    break;
                }
            }
            if (!searchedResult.Any() || !hasExist)
            {
                searchedResult.Add(current);
            }
        }
        var minutes = (DateTime.Now - start).TotalMinutes;

#if DEBUG
        Debug.WriteLine($"耗时 {minutes} minutes");
#endif

        return searchedResult;
    }


    #region 自动化文件读写
    static string _parameterLineStartPattern = string.Empty;
    static string[] _configItemKeys = [];
    /// <summary>
    /// 解析{key}和{{key}}的全局变量
    /// </summary>
    /// <returns></returns>
    private static readonly Regex GlobalVariablesPattern = new(@"(?<!\$)\{\{{0,1}(?<key>[^\s;]+?)(\[(?<index>\d+)\]){0,1}\}\}{0,1}(?<CallReplaceFunc>\.Replace\(((?<Quotation>""{0,1})(?<First>[\w\s]+)\k<Quotation>,\s*\k<Quotation>(?<Second>[\w\s\?]+)\k<Quotation>)\);){0,1}");

    /// <summary>
    /// IF条件指令
    /// </summary>
    /// <returns></returns>
    private static readonly Regex IfStatementPattern = new(@"#IF:(?<Reversal>\!{0,1})(?<SourceTxt>\w+)\.Contains\((?<SubString>\w+)\)(?<Content>[\w\W]+?)#IFEND");
    /// <summary>
    /// 执行文件写入操作指令
    /// </summary>
    /// <param name="commandContent"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async IAsyncEnumerable<CommandResult> ExecuteAsync(string commandContent)
    {
        Operation selectedOp = ResolveOperation(commandContent);
        await foreach (var stepResult in ExecuteOperationCoreAsync(selectedOp, dryRun: false))
        {
            yield return new CommandResult(true, stepResult.Log);
        }
    }

    /// <summary>
    /// 预览执行结果: 不修改任何文件, 只计算每个步骤改动前后的差异
    /// 与<see cref="ExecuteAsync"/>共用配置解析(ResolveOperation)和改动计算(ComputeModification)逻辑, 仅“是否写入磁盘”不同, 以保证预览结果与真实执行一致
    /// </summary>
    /// <param name="commandContent">文件写入操作指令(已经过外部模板解析)</param>
    /// <returns>每个操作步骤对每个目标文件的改动预览</returns>
    public async Task<List<FileModificationPreview>> PreviewAsync(string commandContent)
    {
        Operation selectedOp = ResolveOperation(commandContent);
        List<FileModificationPreview> previews = [];
        await foreach (var stepResult in ExecuteOperationCoreAsync(selectedOp, dryRun: true))
        {
            previews.Add(new FileModificationPreview
            {
                File = stepResult.File,
                NodeTitle = stepResult.NodeTitle,
                OperationType = stepResult.OperationType.ToString(),
                Changed = stepResult.Changed,
                IsNewFile = stepResult.IsNewFile,
                Log = stepResult.Log,
                Hunks = stepResult.Changed ? DiffHelper.BuildHunks(stepResult.OriginContent, stepResult.NewContent) : []
            });
        }
        return previews;
    }

    /// <summary>
    /// 预览LinePattern正则在目标文件中匹配到的所有内容: 不修改任何文件, 用于验证正则是否有效
    /// 匹配方式与<see cref="ComputeModification"/>中对应操作类型完全一致, 同一个文件的后续步骤也会基于前面步骤的结果匹配(与真实执行一致)
    /// </summary>
    /// <param name="commandContent">文件写入操作指令(已经过外部模板解析)</param>
    /// <returns>每个操作步骤在每个目标文件中的匹配情况</returns>
    public async Task<List<LinePatternMatchPreview>> PreviewLinePatternMatchesAsync(string commandContent)
    {
        Operation selectedOp = ResolveOperation(commandContent);
        await ResolveOperationVariablesAsync(selectedOp);

        List<LinePatternMatchPreview> previews = [];
        var files = GetWorkingDirFiles(selectedOp);
        // 同一个文件的多个步骤: 后面的步骤需要在前面步骤的结果上匹配, 才能与真实执行一致
        Dictionary<string, string> simulatedContents = [];
        foreach (var opNode in selectedOp.Nodes)
        {
            var targetFiles = ResolveTargetFiles(selectedOp, opNode, files, dryRun: true);
            foreach (var targetFile in targetFiles)
            {
                ResolveFileNamespace(targetFile, files);
                opNode.NodeTitle = ResolveGlobalVariables(opNode.NodeTitle);
                foreach (var step in opNode.Steps)
                {
                    step.Value = ResolveGlobalVariables(step.Value);

                    string content;
                    if (simulatedContents.TryGetValue(targetFile, out var simulatedContent))
                    {
                        content = simulatedContent;
                    }
                    else
                    {
                        content = File.Exists(targetFile) ? await File.ReadAllTextAsync(targetFile) : string.Empty;
                    }

                    LinePatternMatchPreview preview = new()
                    {
                        File = targetFile,
                        NodeTitle = opNode.NodeTitle,
                        OperationType = step.OperationType.ToString(),
                        LinePattern = step.LinePattern
                    };
                    try
                    {
                        var (matchScope, matches, note) = CollectLinePatternMatches(content, step.LinePattern, step.OperationType);
                        preview.MatchScope = matchScope;
                        preview.Matches = matches;
                        preview.Note = note;
                        if (matchScope != LinePatternMatchScopes.None && matches.Count == 0)
                        {
                            preview.Error = $"没有匹配到任何内容, 真实执行会失败或者不产生改动: {step.LinePattern}";
                        }
                    }
                    catch (Exception ex)
                    {
                        // 无效的正则表达式(调试LinePattern时很常见), 不能中断整个预览
                        preview.MatchScope = LinePatternMatchScopes.None;
                        preview.Error = $"正则表达式无效: {ex.Message}";
                    }
                    previews.Add(preview);

                    try
                    {
                        // 推进模拟内容: 与真实执行一样, 让后续步骤在本步骤的结果上匹配
                        var (newContent, changed, _) = ComputeModification(targetFile, opNode.NodeTitle, step.Value, step.LinePattern, step.OperationType, content);
                        if (changed)
                        {
                            simulatedContents[targetFile] = newContent;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 本步骤真实执行时会失败(已经记录到preview.Error), 后续步骤基于未改动的内容继续匹配
                        LoggerHelper.LogInformation($"预览正则匹配: 步骤\"{opNode.NodeTitle}\"计算改动失败({ex.Message}), 后续步骤基于未改动内容继续匹配");
                    }
                }
                // "NAMESPACE"变量值每个文件需要实时解析, 不删除会导致下个文件会提前被解析出当前值
                _variables.Remove("NAMESPACE");
            }
        }
        return previews;
    }

    /// <summary>
    /// 解析指令文本得到待执行的操作(真实执行与预览共用)
    /// </summary>
    /// <param name="commandContent"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    Operation ResolveOperation(string commandContent)
    {
        if (string.IsNullOrWhiteSpace(_parameterLineStartPattern))
        {
            var props = typeof(OperationNode).GetProperties();
            var stepProps = typeof(NodeStep).GetProperties();
            string propsPattern = string.Join('|', props.Select(x => x.Name)) + "|" + string.Join('|', stepProps.Select(x => x.Name));

            _parameterLineStartPattern = $@"^-{{0,1}}\s*(?<paramName>{propsPattern}):\s*";
            _configItemKeys = [.. props.Select(x => x.Name), .. stepProps.Select(x => x.Name)];
        }
        
        // 匹配操作名称和工作目录
        Operation selectedOp = new();
        string[] titleLines = Regex.Match(commandContent, @"##[\w\W]+?(?=###)").Value.Split('\n');
        // selectedOp.GlobalVariables中只有函数调用生成全局变量的表达式
        selectedOp.GlobalVariables = [.. titleLines.Skip(1)];

        #region RazorEngine解析模板
        Match useRazorEngineMatch = Regex.Match(commandContent, @"\s*ENGINE:\s*Razor\s+", RegexOptions.IgnoreCase);
        if (useRazorEngineMatch.Success)
        {
            LoggerHelper.LogInformation("FileHelper.ExecuteAsync 使用Razor模板引擎");
            // 使用标题和模板变量作为模板缓存的唯一标识符
            string titleLine = useRazorEngineMatch.Value.Trim();
            string currentTmpl = titleLine + string.Join(string.Empty, selectedOp.GlobalVariables);

            ExpandoObject dataModel = new();
            var dataModelDictionary = (IDictionary<string, object>)dataModel;

            // 从执行上下文获取
            foreach (var kvp in executionContext.EnvironmentVariables)
            {
                dataModelDictionary[kvp.Key] = kvp.Value;
            }

            if (!RazorEngine.Engine.Razor.IsTemplateCached(currentTmpl, null))
            {
                RazorEngine.Engine.Razor.AddTemplate(currentTmpl, commandContent);
                RazorEngine.Engine.Razor.Compile(currentTmpl);
            }
            commandContent = RazorEngine.Engine.Razor.Run(currentTmpl, modelType: null, model: dataModel);
            // 重新获取所有标题行titleLines(可能包含模板变量)
            titleLines = Regex.Match(commandContent, @"##[\w\W]+?(?=###)").Value.Split('\n');
        }
        else
        {
            LoggerHelper.LogInformation("FileHelper.ExecuteAsync 使用TmplHelper模板引擎");
        }
        #endregion

        string firstLineValue = titleLines.First()[2..].Trim();
        Match nameAndWorkingDirMatch = Regex.Match(firstLineValue, @"(?<name>\w+)\s*\((?<workingDir>.*)\)");
        selectedOp.Name = nameAndWorkingDirMatch.Groups["name"].Value;
        selectedOp.WorkingDir = nameAndWorkingDirMatch.Groups["workingDir"].Value;

        // 匹配节点内容(###...)
        var matches = Regex.Matches(commandContent, @"###\s*[\w\W]+?(?=(###|$))");
        foreach (var m in matches.Cast<Match>())
        {
            string nodeConfig = m.Value;

            var opNode = ResolveNodeFromConfig(nodeConfig);
            selectedOp.Nodes.Add(opNode);
        }

        return selectedOp;
    }
    static OperationNode ResolveNodeFromConfig(string nodeConfig)
    {
        // 获取第一个换行符的索引
        var firstLineBreakIndex = nodeConfig.IndexOf('\n');
        var nodeTitle = nodeConfig[..firstLineBreakIndex][3..].Trim();
        nodeConfig = nodeConfig[(firstLineBreakIndex + 1)..].Trim();

        //var configItemMatches = Regex.Matches(nodeConfig, _parameterLineStartPattern);

        string targetFilePattern = string.Empty;
        StringBuilder valueBuilder = new();
        string type = string.Empty;
        string linePattern = string.Empty;
        string[] lines = [.. nodeConfig.Split('\n').Select(x => x.TrimEnd('\r'))]; // windows环境CRLF换行(\r\n), \n分割后行尾会有一个\r, 后面AppendLine会多一个\r, 所以trim掉行尾的\r
        string configItemKey = string.Empty;
        string configItemValue;
        bool isConfigItemFirstLine = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            configItemValue = line;
            string lineConfigItemKey = _configItemKeys.FirstOrDefault(x => line.StartsWith(x + ":", StringComparison.OrdinalIgnoreCase));
            isConfigItemFirstLine = !string.IsNullOrWhiteSpace(lineConfigItemKey);
            if (isConfigItemFirstLine)
            {
                configItemKey = lineConfigItemKey;
                // configItemKey.Length + 1: 加1是因为后面还有一个冒号
                configItemValue = SpaceConstants.ReplaceSpacePlaceholders(line[(lineConfigItemKey.Length + 1)..].Trim());
            }
            if (string.IsNullOrWhiteSpace(configItemKey))
            {
                throw new Exception("配置项的键不能为空");
            }
            if (configItemKey == nameof(OperationNode.TargetFilePattern))
            {
                targetFilePattern = configItemValue;
            }
            else if (configItemKey == nameof(NodeStep.Value))
            {
                // 对于 Value 字段的非首行，也需要处理空格占位符
                if (!isConfigItemFirstLine)
                {
                    configItemValue = SpaceConstants.ReplaceSpacePlaceholders(line);
                }
                valueBuilder.AppendLine(configItemValue);
            }
            else if (configItemKey == nameof(NodeStep.OperationType))
            {
                type = configItemValue;
            }
            else if (configItemKey == nameof(NodeStep.LinePattern))
            {
                linePattern = configItemValue;
            }
            else
            {
                continue;
            }
        }

        OperationNode opNode = new()
        {
            Steps = [],
            NodeTitle = nodeTitle,
            TargetFilePattern = targetFilePattern,
        };
        var values = valueBuilder.ToString().Split("|||");
        var types = type.Split("|||");
        var linePatterns = linePattern.Split("|||");
        for (int j = 0; j < values.Length; j++)
        {
            NodeStep step = new()
            {
                Value = values[j].TrimEnd('\r', '\n'),
                LinePattern = linePatterns[j]
            };
            var opType = types[j].Trim();
            step.OperationType = opType switch
            {
                nameof(OperationType.Append) => OperationType.Append,
                nameof(OperationType.Prepend) => OperationType.Prepend,
                nameof(OperationType.Replace) => OperationType.Replace,
                nameof(OperationType.Override) => OperationType.Override,
                nameof(OperationType.Create) => OperationType.Create,
                _ => throw new Exception($"无效的操作类型:{opType}"),
            };
            opNode.Steps.Add(step);
        }
        return opNode;
    }
    /// <summary>
    /// 大驼峰转小驼峰
    /// </summary>
    /// <param name="origin"></param>
    /// <returns></returns>
    public static string ToCamelCase(string origin) => origin.ToCamelCase();
    /// <summary>
    /// 获取单数的复数形式
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    public static string Pluralize(string word) => word.Pluralize();
    /// <summary>
    /// 获取数据库表对应的实体类代码
    /// conn.DbMaintenance会反射获取数据库对应的实例(如MySqlDbMaintenance)
    /// </summary>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <param name="table"></param>
    /// <returns></returns>
    public async Task<string[]> BuildEntityClassCodeAsync(string connectionString, string table)
    {
        var columnInfos = await DatabaseInfo.GetTableColumnsInfoAsync(connectionString, table);
        #region 表字段分析
        //获取主键类型("int", "string")
        var primaryKey = columnInfos.FirstOrDefault(x => x.IsPK == 1);

        //part1.1:生成实体类属性代码
        StringBuilder propsText = new();
        foreach (var columnInfo in columnInfos)
        {
            LoggerHelper.LogInformation($"{columnInfo.ColumnCode} DataType:{columnInfo.ColumnType}; Length:{columnInfo.ColumnLength}");
        }

        //part1.2:using System;
        string usingSystem = string.Empty;
        //part1.3:using System.ComponentModel.DataAnnotations;

        //part2. 除Id外的属性代码
        StringBuilder dtoExceptIdPropsCode = new();
        StringBuilder dtoExceptIdAndDateTimePropsCode = new();
        string usingDataAnnotation = string.Empty;

        foreach (var columnInfo in columnInfos)
        {
            if (primaryKey is not null && columnInfo.ColumnCode != primaryKey?.ColumnCode)
            {
                if (columnInfo.ColumnType is null)
                {
                    throw new Exception($"字段{columnInfo.ColumnCode}类型为空");
                }
                var propType = columnInfo.ColumnType switch
                {
                    _ when columnInfo.ColumnType.Contains("time", StringComparison.OrdinalIgnoreCase) => "DateTime",
                    _ when columnInfo.ColumnType.Contains("bit", StringComparison.OrdinalIgnoreCase) => "bool",
                    _ when columnInfo.ColumnType.Contains("int", StringComparison.OrdinalIgnoreCase) => "int",
                    _ when columnInfo.ColumnType.Contains("lob", StringComparison.OrdinalIgnoreCase) => "byte[]",
                    _ => "string",
                };
                if (propType == "DateTime" && usingSystem.Length == 0)
                {
                    usingSystem = $"{Environment.NewLine}using System;";
                }
                var stringLengthAttr = !string.IsNullOrWhiteSpace(columnInfo.ColumnLength) && Convert.ToInt32(columnInfo.ColumnLength) > 0 && propType == "string" ? $"{Environment.NewLine}{SpaceConstants.TwoTabsSpaces}[StringLength({columnInfo.ColumnLength})]" : string.Empty;
                if (stringLengthAttr.Length > 0 && usingDataAnnotation.Length == 0)
                {
                    usingDataAnnotation = $"{Environment.NewLine}using System.ComponentModel.DataAnnotations;";
                }

                var columnProp = $$"""
                        {{SpaceConstants.TwoTabsSpaces}}/// <summary>
                        {{SpaceConstants.TwoTabsSpaces}}/// {{columnInfo.Remark}}
                        {{SpaceConstants.TwoTabsSpaces}}/// </summary>{{stringLengthAttr}}
                        {{SpaceConstants.TwoTabsSpaces}}public {{propType}} {{columnInfo.ColumnCode}} { get; set; }
                        """;
                propsText.AppendLine(columnProp);

                string commonPropCode = $$"""
                        {{SpaceConstants.TwoTabsSpaces}}/// <summary>
                        {{SpaceConstants.TwoTabsSpaces}}/// {{columnInfo.Remark}}
                        {{SpaceConstants.TwoTabsSpaces}}/// </summary>
                        {{SpaceConstants.TwoTabsSpaces}}public {{propType}} {{columnInfo.ColumnCode}} { get; set; }
                        """;
                dtoExceptIdPropsCode.AppendLine(commonPropCode);
                if (!propType.Equals("DateTime"))
                {
                    dtoExceptIdAndDateTimePropsCode.AppendLine(commonPropCode);
                }
            }
        }
        #endregion

        #region 生成实体类代码
        string entityCalssName = await GetEntityClassName(table);
        string primaryKeyType = primaryKey?.ColumnType?.Contains("char") == true ? "string" : "int";
        string entityClassCode = $$"""
                using Iduo.Net.Entity;{{usingSystem}}{{usingDataAnnotation}}

                namespace {NAMESPACE}
                {
                    public class {{entityCalssName}} : EntityBase<{{primaryKeyType}}>
                    {
                {{propsText.ToString().TrimEnd()}}
                    }
                }

                """;

        string timeRangeFilterProps = $$"""
                {{SpaceConstants.TwoTabsSpaces}}/// <summary>
                {{SpaceConstants.TwoTabsSpaces}}/// 开始时间
                {{SpaceConstants.TwoTabsSpaces}}/// </summary>
                {{SpaceConstants.TwoTabsSpaces}}public DateTime? StartTime { get; set; }
                {{SpaceConstants.TwoTabsSpaces}}/// <summary>
                {{SpaceConstants.TwoTabsSpaces}}/// 结束时间
                {{SpaceConstants.TwoTabsSpaces}}/// </summary>
                {{SpaceConstants.TwoTabsSpaces}}public DateTime? EndTime { get; set; }
                """;
        #endregion

        return [
            entityCalssName,
            entityClassCode,
            primaryKeyType,
            dtoExceptIdPropsCode.ToString().TrimEnd(),
            dtoExceptIdAndDateTimePropsCode.ToString().TrimEnd(),
            timeRangeFilterProps
        ];
    }

    /// <summary>
    /// 获取数据库表名对应的实体类名称
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    async Task<string> GetEntityClassName(string table)
    {
        if (table.Contains('_'))
        {
            table = table[(table.IndexOf('_') + 1)..];
        }
        string question = $"我的数据库表名为{table},使用PascalCase风格转换为C#的实体类名称(单数形式,不要添加Entry,Model等后缀)应该为?只回答名字即可";
        string answer = await aiService.AskAsync(question);
        int index = answer.IndexOf('\n');
        string entityClassName = index > -1 ? answer[..index].Trim() : answer;
        return entityClassName;
    }

    readonly Dictionary<string, string> _variables = new()
    {
        { "TAB", SpaceConstants.OneTabSpaces }
    };
    void ResolveTargetFileNamespace(string classFileDir, string classFileProjFile)
    {
        #region 解析文件的命名空间(命名空间随着文件位置不同而变化)
        if (!string.IsNullOrWhiteSpace(classFileProjFile))
        {
            // xxx/xxxxx.csproj
            // xxx/
            var csprojDir = classFileProjFile[..(classFileProjFile.LastIndexOf('/') + 1)];
            // =>  xxxxx
            string ns = classFileProjFile.Replace(csprojDir, string.Empty).Replace(".csproj", string.Empty);
            var subDirectoryParts = classFileDir.Replace(csprojDir, string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in subDirectoryParts)
            {
                ns += $".{part}";
            }
            _variables["NAMESPACE"] = $"{ns}";
        }
        #endregion
    }
    /// <summary>
    /// 解析用户输入变量和函数变量(包含全局变量的解析)
    /// </summary>
    /// <param name="opNode"></param>
    /// <returns></returns>
    async Task ResolveVariablesAsync(OperationNode opNode)
    {
        // 解析函数变量(TargetFilePattern和Steps中每个项的Value)
        opNode.TargetFilePattern = await ResolveFunctionVariablesAsync(opNode.TargetFilePattern);
        foreach (var step in opNode.Steps)
        {
            step.LinePattern = await ResolveFunctionVariablesAsync(step.LinePattern);
            step.Value = await ResolveFunctionVariablesAsync(step.Value);
        }

        // 解析条件语句决定是否输出(TargetFilePattern和Steps中每个项的Value)
        opNode.TargetFilePattern = ResolveIfStatement(opNode.TargetFilePattern);
        foreach (var step in opNode.Steps)
        {
            step.Value = ResolveIfStatement(step.Value);
        }
    }
    /// <summary>
    /// 解析用户输入变量和函数变量(包含全局变量的解析)
    /// </summary>
    /// <param name="originTxt"></param>
    /// <returns></returns>
    async Task<string> ResolveVariablesAsync(string originTxt)
    {
        originTxt = await ResolveFunctionVariablesAsync(originTxt);
        return originTxt;
    }

    /// <summary>
    /// 解析文本中的函数变量(调用获取函数输出)
    /// </summary>
    /// <param name="originTxt"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    async Task<string> ResolveFunctionVariablesAsync(string originTxt)
    {
        // 语法：FuncName(params) => A, B, C 或 FuncName(params) => [0]
        // 旧格式兼容：FuncName(params)->Key->["A", "B"] 会忽略 Key
        var matches = originTxt.ResolvePairedSymbolsContent("\\(", "\\)", "(?<FunctionName>[A-Z]\\w+)", "(->\\w+)?(->|\\s*=>\\s*)\\[?(?<index>\\d+|[,A-Za-z,\\s,\"]+)\\]?");
        foreach (Match functionMatch in matches)
        {
            var functionName = functionMatch.Groups["FunctionName"].Value;
            var parametersTxt = functionMatch.Groups["x"].Value;
            // 解析全局变量: {VarName}和已经解析的用户输入变量{{VarName}}
            var resolvedParametersTxt = ResolveGlobalVariables(parametersTxt);
            if (_variables.ContainsKey(functionName) && int.TryParse(parametersTxt, out _))
            {
                continue;
            }
            string val = string.Empty;
            // 使用 ||| 分隔参数（避免与代码中的逗号冲突）
            string[] parameters = resolvedParametersTxt.Split("|||");
            MethodInfo? method = typeof(FileHelper).GetMethods().FirstOrDefault(x => x.Name == functionName) ?? throw new Exception($"没有找到方法:{functionName}");
            LoggerHelper.LogInformation($"即将调用方法{functionName}, 参数:");
            foreach (var parameter in parameters)
            {
                LoggerHelper.LogInformation($"{SpaceConstants.OneTabSpaces} - {parameter}");
            }

            var methodResult = method.IsStatic ? method.Invoke(null, parameters) : method.Invoke(null, parameters) ?? throw new Exception($"方法{functionName}调用异常");
            if (functionName.EndsWith("Async"))
            {
                if (methodResult is Task<string[]> resultTask)
                {
                    val = string.Join("|||", await resultTask);
                }
                else if (methodResult is Task<string> stringResultTask)
                {
                    val = await stringResultTask;
                }
                else
                {
                    throw new Exception($"暂时不支持方法{functionName}返回值类型{methodResult.GetType().Name}");
                }
            }
            else
            {
                val = methodResult is string[] result
                    ? string.Join("|||", result)
                    : methodResult?.ToString() ?? throw new Exception($"方法{functionName}返回值为空");
            }

            var index = functionMatch.Groups["index"].Value;
            if (int.TryParse(index, out int indexIntVal))
            {
                // 数字索引：取返回值数组的第 n 个元素
                originTxt = originTxt.Replace(functionMatch.Value, val.Split("|||")[indexIntVal]);
            }
            else
            {
                // 变量名列表：将返回值数组各元素分别保存到变量中
                var vals = val.Split("|||");
                var varNames = index.Split(',').Select(x => x.Trim().Trim('"')).ToArray();

                for (int i = 0; i < varNames.Length && i < vals.Length; i++)
                {
                    if (!_variables.ContainsKey(varNames[i]))
                    {
                        _variables[varNames[i]] = vals[i];
                    }
                }

                return val;
            }
        }
        // 函数输出依然可能包含全局变量
        originTxt = ResolveGlobalVariables(originTxt);

        return originTxt;
    }
    /// <summary>
    /// 解析根据条件判断是否需要输出的文本
    /// </summary>
    /// <param name="originTxt"></param>
    /// <returns></returns>
    string ResolveIfStatement(string originTxt)
    {
        var ifMatches = IfStatementPattern.Matches(originTxt);
        foreach (Match m in ifMatches)
        {
            var hasReversalSymbol = m.Groups["Reversal"].Value;
            var contains = string.IsNullOrWhiteSpace(hasReversalSymbol);

            var source = m.Groups["SourceTxt"].Value;
            var sourceValue = _variables[source];

            var subString = m.Groups["SubString"].Value;

            var ifContent = m.Groups["Content"].Value;
            var elseContent = string.Empty;
            if (ifContent.Contains("#ELSE:"))
            {
                var arr = ifContent.Split("#ELSE:");
                ifContent = arr[0];
                elseContent = arr[1];
            }

            if (contains && sourceValue.Contains(subString) || !contains && !sourceValue.Contains(subString))
            {
                // 条件为真, content为需要内容
                originTxt = originTxt.Replace(m.Value, ifContent);
            }
            else
            {
                // 条件为假, content不需要
                originTxt = originTxt.Replace(m.Value, elseContent);
            }
        }

        return originTxt;
    }

    /// <summary>
    /// 解析文本中的全局变量
    /// </summary>
    /// <param name="originTxt"></param>
    /// <returns></returns>
    string ResolveGlobalVariables(string originTxt)
    {
        // 获取所有匹配的全局变量并去重
        var allMatches = GlobalVariablesPattern.Matches(originTxt);
        List<Match> matches = [];
        foreach (Match match in allMatches)
        {
            if (!matches.Any(x => x.Value == match.Value))
            {
                matches.Add(match);
            }
        }

        // 替换全局变量
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            if (_variables.TryGetValue(key, out var value))
            {
                var index = match.Groups["index"].Value;
                string val = string.IsNullOrWhiteSpace(index) ? value : value.Split("|||")[Convert.ToInt32(index)];

                #region 解析Replace函数
                // .Replace("xx", "xxx");
                var callReplaceFuncStatement = match.Groups["CallReplaceFunc"].Value;
                string first = match.Groups["First"].Value;
                string second = match.Groups["Second"].Value;
                if (!string.IsNullOrWhiteSpace(callReplaceFuncStatement) && !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second))
                {
                    val = val.Replace(first, second);
                }
                #endregion

                originTxt = originTxt.Replace(match.Value, val);
            }
        }
        return originTxt;
    }

    static (string, string, int) GetNextParameter(string[] settingLines, int lineIndex)
    {
        lineIndex++;
        if (lineIndex >= settingLines.Length || settingLines[lineIndex].Trim().StartsWith("###"))
        {
            lineIndex--;
            return (string.Empty, string.Empty, lineIndex);
        }

        int f = -2;
        string lineParamName = string.Empty;
        string lineParamValue = string.Empty;
        while (true)
        {
            if (lineIndex >= settingLines.Length)
            {
                lineIndex--;
                break;
            }
            string val = settingLines[lineIndex];
            var paramStartFlagMatch = Regex.Match(val, _parameterLineStartPattern);
            if (paramStartFlagMatch.Success)
            {
                f++;
                val = val.Replace(paramStartFlagMatch.Value, string.Empty);
                if (string.IsNullOrWhiteSpace(lineParamName))
                {
                    lineParamName = paramStartFlagMatch.Groups["paramName"].Value;
                }
            }
            else
            {
                if (Regex.IsMatch(val, _parameterLineStartPattern) || Regex.IsMatch(val, @"\s*###"))
                {
                    lineIndex--;
                    break;
                }
            }
            if (f == 0)
            {
                lineIndex--;
                break;
            }
            else if (f == -1)
            {
                lineParamValue = lineParamValue.Length == 0 ? val : $"{lineParamValue}{Environment.NewLine}{val}";
            }
            if (lineParamName != nameof(NodeStep.Value))
            {
                break;
            }
            lineIndex++;
        }

        return (lineParamName, lineParamValue, lineIndex);
    }

    /// <summary>
    /// 一个操作步骤对一个文件的执行结果(真实执行与预览共用)
    /// </summary>
    class StepModificationResult
    {
        public string File { get; set; } = string.Empty;
        public string NodeTitle { get; set; } = string.Empty;
        public OperationType OperationType { get; set; }
        /// <summary>是否产生改动(false表示已经是目标状态, 真实执行也会跳过)</summary>
        public bool Changed { get; set; }
        /// <summary>目标文件当前是否不存在</summary>
        public bool IsNewFile { get; set; }
        public string OriginContent { get; set; } = string.Empty;
        public string NewContent { get; set; } = string.Empty;
        public string Log { get; set; } = string.Empty;
    }

    /// <summary>
    /// 解析操作中的所有变量(真实执行与预览共用)
    /// </summary>
    async Task ResolveOperationVariablesAsync(Operation selectedOp)
    {
        // 预设全局变量
        foreach (var variable in selectedOp.GlobalVariables)
        {
            _ = await ResolveVariablesAsync(variable);
        }

        foreach (var opNode in selectedOp.Nodes)
        {
            await ResolveVariablesAsync(opNode);
        }
    }

    /// <summary>
    /// 获取工作目录中的所有文件(真实执行与预览共用)
    /// </summary>
    static IEnumerable<string> GetWorkingDirFiles(Operation selectedOp)
    {
        if (string.IsNullOrWhiteSpace(selectedOp.WorkingDir))
        {
            throw new Exception($"{selectedOp.Name}缺少工作目录配置");
        }
        return GetSourceCodeFiles(selectedOp.WorkingDir);
    }

    /// <summary>
    /// 解析操作节点的目标文件(真实执行与预览共用)
    /// </summary>
    /// <param name="selectedOp">待执行的操作</param>
    /// <param name="opNode">当前操作节点</param>
    /// <param name="files">工作目录中的所有文件</param>
    /// <param name="dryRun">true表示预览: 不会真的创建文件</param>
    static IEnumerable<string> ResolveTargetFiles(Operation selectedOp, OperationNode opNode, IEnumerable<string> files, bool dryRun)
    {
        // 多个target(比如index.html和App.razor文件同时)需要执行多个步骤Step(比如添加css引用和js引用)
        IEnumerable<string> targetFiles;
        var createFileSteps = opNode.Steps.Where(x => x.OperationType == OperationType.Create);
        if (createFileSteps.Count() > 1)
        {
            throw new Exception($"创建文件操作\"{opNode.NodeTitle}\"只能包含一个步骤");
        }
        else if (createFileSteps.Count() == 1)
        {
            int lastSlashIndex = opNode.TargetFilePattern.LastIndexOf('/');
            var creatingFile = opNode.TargetFilePattern[(lastSlashIndex + 1)..];
            if (string.IsNullOrWhiteSpace(creatingFile))
            {
                throw new Exception($"\"{opNode.NodeTitle}\"没有匹配到需要创建的文件名:{opNode.TargetFilePattern}");
            }
            string creatingFilePath;
            if (opNode.TargetFilePattern.Contains('/'))
            {
                string? creatingFileDir = opNode.TargetFilePattern[..lastSlashIndex] + '/';
                string? creatingFileDirFullPath = files.FirstOrDefault(x => Regex.IsMatch(x, creatingFileDir));
                if (string.IsNullOrWhiteSpace(creatingFileDirFullPath))
                {
                    LoggerHelper.LogCritical($"当前操作{opNode.NodeTitle}不执行, 因为工作目录{selectedOp.WorkingDir}中没有找到需要创建文件:{opNode.TargetFilePattern}");
                }
                creatingFileDirFullPath = creatingFileDirFullPath?[..creatingFileDirFullPath.LastIndexOf('/')];
                creatingFilePath = $"{creatingFileDirFullPath}/{creatingFile}";
            }
            else
            {
                creatingFilePath = Path.Combine(selectedOp.WorkingDir, opNode.TargetFilePattern);
            }

            if (!File.Exists(creatingFilePath) && !dryRun)
            {
                File.Create(creatingFilePath).Close();
            }
            targetFiles = [creatingFilePath];
        }
        else
        {
            targetFiles = files.Where(x => Regex.IsMatch(x, opNode.TargetFilePattern, RegexOptions.IgnoreCase));
        }
        if (!targetFiles.Any())
        {
            throw new Exception($"没有找到文件:{opNode.TargetFilePattern}");
        }
        return targetFiles;
    }

    /// <summary>
    /// 解析目标文件所属项目的命名空间(供NAMESPACE变量使用; 真实执行与预览共用)
    /// </summary>
    void ResolveFileNamespace(string targetFile, IEnumerable<string> files)
    {
        var csprojFile = GetFileProjectFile(targetFile, files);
        if (!string.IsNullOrWhiteSpace(csprojFile))
        {
            var targetFileDir = targetFile[..(targetFile.LastIndexOf('/') + 1)];
            ResolveTargetFileNamespace(targetFileDir, csprojFile);
        }
    }

    // string file, string value, string operationTitle, OperationType operationType, string appendedLinePattern = ""
    /// <summary>执行用户选择的操作(所有子命令)</summary>
    /// <param name="selectedOp">待执行的操作</param>
    /// <param name="dryRun">true表示预览: 不创建文件也不写入文件, 其余逻辑与真实执行完全相同</param>
    async IAsyncEnumerable<StepModificationResult> ExecuteOperationCoreAsync(Operation selectedOp, bool dryRun)
    {
        await ResolveOperationVariablesAsync(selectedOp);

        // 预览时文件不落盘, 同一个文件的多个步骤需要基于前一步的结果继续计算, 才能与真实执行一致
        Dictionary<string, string> simulatedContents = [];
        var files = GetWorkingDirFiles(selectedOp);
        foreach (var opNode in selectedOp.Nodes)
        {
            var targetFiles = ResolveTargetFiles(selectedOp, opNode, files, dryRun);
            foreach (var targetFile in targetFiles)
            {
                ResolveFileNamespace(targetFile, files);
                opNode.NodeTitle = ResolveGlobalVariables(opNode.NodeTitle);
                opNode.TargetFilePattern = ResolveGlobalVariables(opNode.TargetFilePattern);
                foreach (var step in opNode.Steps)
                {
                    step.Value = ResolveGlobalVariables(step.Value);

                    bool isNewFile = !File.Exists(targetFile);
                    string originContent;
                    if (simulatedContents.TryGetValue(targetFile, out var simulatedContent))
                    {
                        originContent = simulatedContent;
                    }
                    else if (isNewFile)
                    {
                        // 预览Create操作时目标文件还没有创建, 内容视为空(与真实执行时创建出的空文件等价)
                        originContent = string.Empty;
                    }
                    else
                    {
                        originContent = await File.ReadAllTextAsync(targetFile);
                    }

                    var (newContent, changed, opLog) = ComputeModification(targetFile, opNode.NodeTitle, step.Value, step.LinePattern, step.OperationType, originContent);
                    if (changed)
                    {
                        if (dryRun)
                        {
                            simulatedContents[targetFile] = newContent;
                        }
                        else
                        {
                            await File.WriteAllTextAsync(targetFile, newContent);
                        }
                    }
                    LoggerHelper.LogInformation(opLog);

                    yield return new StepModificationResult
                    {
                        File = targetFile,
                        NodeTitle = opNode.NodeTitle,
                        OperationType = step.OperationType,
                        Changed = changed,
                        IsNewFile = isNewFile,
                        OriginContent = originContent,
                        NewContent = newContent,
                        Log = opLog
                    };
                }
                // "NAMESPACE"变量值每个文件需要实时解析, 不删除会导致下个文件会提前被解析出当前值
                _variables.Remove("NAMESPACE");
            }
        }
    }
    /// <summary>
    /// 计算一个操作步骤作用在指定内容上的结果(纯计算, 不读写文件; 真实执行与预览共用, 保证二者结果一致)
    /// </summary>
    /// <param name="file">目标文件(仅用于日志)</param>
    /// <param name="operationTitle">操作标题(仅用于日志)</param>
    /// <param name="value">要写入的内容</param>
    /// <param name="appendedLinePattern">定位行的正则表达式</param>
    /// <param name="operationType">操作方式</param>
    /// <param name="content">改动前的内容</param>
    /// <returns>NewContent-改动后的内容(无需改动时为原内容); Changed-是否需要改动; Log-操作日志</returns>
    static (string NewContent, bool Changed, string Log) ComputeModification(string file, string operationTitle, string value, string appendedLinePattern, OperationType operationType, string content)
    {
        // 在实际使用 value 前替换换行占位符
        value = SpaceConstants.ReplaceBreakPlaceholders(value);
        StringBuilder operationLog = new();
        if ((operationType == OperationType.Append || operationType == OperationType.Prepend) && content.Contains(value))
        {
            operationLog.AppendLine($"- 已\"{operationTitle}\", 无需操作 ({file})");
            return (content, false, operationLog.ToString());
        }
        else
        {
            string newContent;
            if (operationType == OperationType.Override || operationType == OperationType.Create)
            {
                if (value == content)
                {
                    operationLog.AppendLine($"- 已\"{operationTitle}\", 无需操作 ({file})");
                    return (content, false, operationLog.ToString());
                }
                newContent = value;
            }
            else if (operationType == OperationType.Replace)
            {
                if (!string.IsNullOrWhiteSpace(value) && content.Contains(value))
                {
                    operationLog.AppendLine($"- 已\"{operationTitle}\", 无需操作 ({file})");
                    return (content, false, operationLog.ToString());
                }
                newContent = Regex.Replace(content, appendedLinePattern, value);
            }
            else
            {
                var lines = content.Split('\n').Select(x => x.TrimEnd()).ToList();
                if (string.IsNullOrWhiteSpace(appendedLinePattern))
                {
                    // 计算末尾空字符串的数量
                    int trailingEmptyCount = 0;
                    for (int i = lines.Count - 1; i >= 0; i--)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i]))
                            trailingEmptyCount++;
                        else
                            break;
                    }
                    // 移除末尾空字符串
                    for (int i = 0; i < trailingEmptyCount; i++)
                        lines.RemoveAt(lines.Count - 1);
                    // 添加一个空字符串（与前面内容的空行分隔）
                    lines.Add("");
                    // 添加值
                    lines.Add(value);
                    // 恢复原来的末尾空字符串数量
                    for (int i = 0; i < trailingEmptyCount; i++)
                        lines.Add("");
                }
                else
                {
                    var appendedLine = lines.LastOrDefault(x => Regex.IsMatch(x, appendedLinePattern));
                    if (string.IsNullOrWhiteSpace(appendedLine))
                    {
                        throw new Exception($"没有匹配到对应的行:{appendedLinePattern}");
                    }
                    var appendedLineIndex = lines.IndexOf(appendedLine);
                    if (operationType == OperationType.Append)
                    {
                        lines.Insert(appendedLineIndex + 1, value);
                        operationLog.AppendLine($"- {file} Append Line: \"{value}\"");
                    }
                    else if (operationType == OperationType.Prepend)
                    {
                        lines.Insert(appendedLineIndex, value);
                    }
                    else
                    {
                        throw new Exception($"无效的操作方式:{operationType}");
                    }
                }
                newContent = string.Join(Environment.NewLine, lines);
            }

            operationLog.AppendLine($"√ 操作成功: \"{operationTitle}\" ({file})");
            return (newContent, true, operationLog.ToString());
        }
    }
    /// <summary>
    /// 收集LinePattern在指定内容中的所有匹配(纯计算, 不读写文件)
    /// 匹配方式与<see cref="ComputeModification"/>中对应操作类型的分支完全一致, 保证预览出的匹配就是真实执行时用到的匹配
    /// </summary>
    /// <param name="content">目标文件当前的内容</param>
    /// <param name="linePattern">定位行的正则表达式</param>
    /// <param name="operationType">操作方式</param>
    /// <returns>MatchScope-匹配范围; Matches-所有匹配项; Note-匹配行为的补充说明</returns>
    static (string MatchScope, List<LinePatternMatch> Matches, string Note) CollectLinePatternMatches(string content, string linePattern, OperationType operationType)
    {
        if (operationType == OperationType.Override || operationType == OperationType.Create)
        {
            return (LinePatternMatchScopes.None, [], $"{operationType}操作直接写入整个文件, 不使用LinePattern");
        }
        if (string.IsNullOrWhiteSpace(linePattern))
        {
            return (LinePatternMatchScopes.None, [], "没有配置LinePattern, 内容将被追加到文件末尾");
        }

        List<LinePatternMatch> matches = [];
        if (operationType == OperationType.Replace)
        {
            // Replace是在整个文件内容上执行Regex.Replace, 匹配可以跨行, 并且所有匹配都会被替换
            foreach (Match match in Regex.Matches(content, linePattern))
            {
                matches.Add(new LinePatternMatch
                {
                    LineNumber = content[..match.Index].Count(x => x == '\n') + 1,
                    Text = match.Value,
                    IsAnchor = true
                });
            }
            return (LinePatternMatchScopes.Content, matches, matches.Count > 1 ? $"Replace会替换全部{matches.Count}处匹配" : string.Empty);
        }

        // Append/Prepend是按行匹配: 内容按'\n'分割并去掉行尾空白后逐行匹配
        var lines = content.Split('\n').Select(x => x.TrimEnd()).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], linePattern))
            {
                matches.Add(new LinePatternMatch { LineNumber = i + 1, Text = lines[i] });
            }
        }
        if (matches.Count == 0)
        {
            return (LinePatternMatchScopes.Line, matches, string.Empty);
        }

        // 真实执行取最后一个匹配行作为定位锚点
        var anchorLine = lines.LastOrDefault(x => Regex.IsMatch(x, linePattern));
        if (string.IsNullOrWhiteSpace(anchorLine))
        {
            return (LinePatternMatchScopes.Line, matches, "匹配到的行是空白行, 真实执行会因为找不到定位行而失败");
        }
        // 真实执行是用行内容(而不是行号)反查插入位置, 存在内容完全相同的行时定位到的是第一个
        int anchorLineNumber = lines.IndexOf(anchorLine) + 1;
        var anchorMatch = matches.FirstOrDefault(x => x.LineNumber == anchorLineNumber);
        if (anchorMatch is not null)
        {
            anchorMatch.IsAnchor = true;
        }
        string note = anchorLineNumber == matches[matches.Count - 1].LineNumber
            ? string.Empty
            : $"存在内容完全相同的行, 真实执行会定位到第{anchorLineNumber}行(而不是最后一个匹配行)";
        return (LinePatternMatchScopes.Line, matches, note);
    }

    enum OperationType
    {
        Append,
        Prepend,
        Replace,
        Override,
        Create
    }

    class Operation
    {
        /// <summary>
        /// 预设全局变量, 支持用户输入值, 支持函数调用输出值
        /// </summary>
        public List<string> GlobalVariables { get; set; } = [];
        /// <summary>操作名称</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>指定解决方案目录</summary>
        public string WorkingDir { get; set; } = string.Empty;
        /// <summary>具体需要执行的哪些指令</summary>
        public List<OperationNode> Nodes { get; set; } = [];
    }
    class OperationNode
    {
        public string NodeTitle { get; set; } = string.Empty;
        /// <summary>
        /// Create创建文件时为目录名; 其他情况是匹配文件名的正则表达式
        /// </summary>
        public string TargetFilePattern { get; set; } = string.Empty;

        public List<NodeStep> Steps { get; set; } = [];
    }
    class NodeStep
    {
        /// <summary>要添加的文本</summary>
        public string Value { get; set; } = string.Empty;
        /// <summary>使用正则定位到行</summary>
        public string LinePattern { get; set; } = string.Empty;
        /// <summary>
        /// 操作方式:
        /// Append: 定位行的后面添加Value;
        /// Prepend: 定位行的前面添加Value;
        /// Replace: 替换定位的部分(可能多行);
        /// Override: 覆盖, 即将所有内容替换为Value(或创建文件)
        /// </summary>
        public OperationType OperationType { get; set; }
    }

    /// <summary>
    /// 获取指定目录下的所有源代码文件
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetSourceCodeFiles(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var files = Directory.GetFiles(dir, "*", new EnumerationOptions() { RecurseSubdirectories = true })
            .Select(x => x.Replace('\\', '/'))
            .Where(x => !x.Contains("/obj/") && !x.Contains("/bin/"));
        return files;
    }
    /// <summary>
    /// 获取指定目录的文件信息
    /// </summary>
    /// <param name="dir">指定目录</param>
    /// <param name="patterns">正则表达式</param>
    /// <returns>每个正则表达式都返回第一个匹配的结果</returns>
    /// <exception cref="Exception"></exception>
    public static string[] GetDirectoryFileInfo(string dir, string patterns)
    {
        List<string> matchedResult = [];
        var allFiles = GetSourceCodeFiles(dir);
        foreach (var item in patterns.Split(',', ';'))
        {
            string result = string.Empty;
            foreach (var filePath in allFiles)
            {
                Match m = Regex.Match(filePath, item);
                if (m.Success)
                {
                    result = m.Value;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new Exception($"在目录{dir}中没有找到匹配{item}的文件");
            }
            matchedResult.Add(result);
        }
        return [.. matchedResult];
    }
    /// <summary>
    /// 获取项目目录
    /// </summary>
    /// <param name="workingDir">指定目录</param>
    /// <param name="pattern">正则表达式</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string[] GetFileProjDirAndNamespace(string workingDir, string pattern)
    {
        var sourceFiles = GetSourceCodeFiles(workingDir);
        foreach (var sourceFile in sourceFiles)
        {
            var m = Regex.Match(sourceFile, pattern, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var proj = GetFileProjectFile(sourceFile, sourceFiles);
                string projDir = proj[..(proj.LastIndexOf('/') + 1)];
                var serviceRootNS = proj.Replace(projDir, string.Empty).Replace(".csproj", string.Empty);
                return [projDir, serviceRootNS];
            }
        }
        throw new Exception("获取项目目录失败");
    }
    /// <summary>
    /// 构建WhereIf链式语句
    /// </summary>
    /// <param name="propsCode"></param>
    /// <returns></returns>
    public static string BuildWhereIfStatement(string propsCode)
    {
        var matches = Regex.Matches(propsCode, @"public\s+(?<PropType>\w+)\s+(?<PropName>\w+)\s+\{");
        List<Tuple<string, string>> cols = [];
        foreach (Match match in matches.Cast<Match>())
        {
            string propType = match.Groups["PropType"].Value;
            string propName = match.Groups["PropName"].Value;
            cols.Add(Tuple.Create(propType, propName));
        }

        var keywordQueryStatements = cols
            .Where(x => x.Item1.Equals("string") && x.Item2.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"x.{x.Item2}.Contains(search.KeyWord)");
        string keywordStatement = string.Join(" || ", keywordQueryStatements);
        string statement = !string.IsNullOrWhiteSpace(keywordStatement) ?
            $".WhereIf(x => {keywordStatement}, !string.IsNullOrWhiteSpace(search.KeyWord)){Environment.NewLine}"
            : "";
        foreach (var item in cols)
        {
            if (item.Item1.Equals("string"))
            {
                string compareMethodName = item.Item2.EndsWith("id", StringComparison.OrdinalIgnoreCase) ? "Equals" : "Contains";
                statement += $"{SpaceConstants.FourTabsSpaces}.WhereIf(x => x.{item.Item2}.{compareMethodName}(search.{item.Item2}), !string.IsNullOrWhiteSpace(search.{item.Item2})){Environment.NewLine}";
            }
            else
            {
                statement += $"{SpaceConstants.FourTabsSpaces}.WhereIf(x => x.{item.Item2} == search.{item.Item2}, search.{item.Item2}.HasValue){Environment.NewLine}";
            }
        }
        return statement.TrimEnd();
    }
    /// <summary>
    /// 获取指定文件的项目文件
    /// </summary>
    /// <param name="targetFile">指定文件</param>
    /// <param name="files">解决方案中的所有文件路径(目录分隔符为'/')</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string GetFileProjectFile(string targetFile, IEnumerable<string> files)
    {
        // xxx/Entities/Test.cs
        // xxx/Entities/
        string serviceFileDir = targetFile[..(targetFile.LastIndexOf('/') + 1)];
        var csprojFile = files.FirstOrDefault(x => x.EndsWith(".csproj") && serviceFileDir.StartsWith(x[..(x.LastIndexOf('/') + 1)]));
        return csprojFile;
    }
    /// <summary>
    /// 获取时间属性赋值代码
    /// </summary>
    /// <param name="entityPropsCode"></param>
    /// <returns></returns>
    public static string[] GetDateTimePropAssignCode(string entityPropsCode)
    {
        string assignDateTimeWhenAddEntity = string.Empty;
        if (entityPropsCode.Contains("CreateTime"))
        {
            assignDateTimeWhenAddEntity = $"entity.CreateTime = DateTime.Now;";
        }
        if (entityPropsCode.Contains("UpdateTime"))
        {
            string updateStatement = "entity.UpdateTime = DateTime.Now;";
            assignDateTimeWhenAddEntity += string.IsNullOrEmpty(assignDateTimeWhenAddEntity) ? updateStatement : $"{Environment.NewLine}{SpaceConstants.ThreeTabsSpaces}{updateStatement}";
        }

        string assignDateTimeWhenUpdateEntity = string.Empty;
        if (entityPropsCode.Contains("CreateTime"))
        {
            assignDateTimeWhenUpdateEntity = $"updatingEntity.CreateTime = entity.CreateTime;";
        }
        if (entityPropsCode.Contains("UpdateTime"))
        {
            string updateStatement = "updatingEntity.UpdateTime = DateTime.Now;";
            assignDateTimeWhenUpdateEntity += string.IsNullOrEmpty(assignDateTimeWhenUpdateEntity) ? updateStatement : $"{Environment.NewLine}{SpaceConstants.ThreeTabsSpaces}{updateStatement}";
        }

        return [assignDateTimeWhenAddEntity, assignDateTimeWhenUpdateEntity];
    }
    #endregion
}

/// <summary>
/// 源文件类型
/// </summary>
public enum SourceFileType
{
    /// <summary>
    /// 配置文件
    /// </summary>
    Appsettings
}

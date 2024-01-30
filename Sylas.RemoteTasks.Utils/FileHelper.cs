using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.RegexExp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils;
/// <summary>
/// 文件操作帮助类
/// </summary>
public partial class FileHelper
{
    private static readonly string _oneTabSpace = new(' ', 4);
    private static readonly string _twoTabsSpace = new(' ', 8);
    private static readonly string _fourTabsSpace = new(' ', 16);
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
    public static List<string> GetDirectorysRecursive(string dir,
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
        if (filter == null)
        {
            filter = s => true;
        }
        if (dirs == null)
        {
            dirs = new List<string>();
        }
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
            GetDirectorysRecursive(dirItem, filter, stopRecursionConditionOfDirs, dirs);
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

        using (var streamWriter = new StreamWriter(file, false, new UTF8Encoding(false)))
        {
            streamWriter.Write(newCon);
            streamWriter.Flush();
            streamWriter.Close();
        }
    }

    /// <summary>
    /// 修改json文件, 不能添加一个对象节点×××××××xxxx
    /// </summary>
    /// <param name="settingsFile"></param>
    /// <param name="settingsUpdater"></param>
    public static void SettingsWrite(string settingsFile, Action<dynamic> settingsUpdater)
    {
        if (!File.Exists(settingsFile))
        {
            throw new ArgumentException($"配置文件{settingsFile}不存在");
        }
        using (StreamReader sr = new StreamReader(settingsFile, new UTF8Encoding(false)))
        {
            string settings = sr.ReadToEnd();
            // 将配置文件内容反序列化为对象
            dynamic settingsObj = JsonConvert.DeserializeObject(settings) ?? throw new Exception($"反序列化失败");
            // 对配置文件内容进行更新操作
            settingsUpdater(settingsObj);

            string updatedSettings = JsonConvert.SerializeObject(settingsObj);
            if (updatedSettings.Length + 20 < UnformatJsonString(settings).Length)
            {
                throw new Exception("json配置文件的配置减少超过20个字符, 如果您在修改某些配置, 为了防止未知bug引起配置的清空, 建议您手动修改, 否则请修改源代码");
            }
            sr.Close();
            using StreamWriter sw = new StreamWriter(settingsFile, false, new UTF8Encoding(false));
            sw.Write(updatedSettings);
            sw.Flush();
            sw.Close();
        }
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
        using (StreamReader sr = new StreamReader(settingsFile, new UTF8Encoding(false)))
        {
            string settings = sr.ReadToEnd();
            // 将配置文件内容反序列化为对象
            settingsObj = JsonConvert.DeserializeObject(settings);
            sr.Close();
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
    /// 插入代码 - 属性
    /// </summary>
    /// <param name="classFile"></param>
    /// <param name="codes"></param>
    public static void InsertCodePropertyLevel(string classFile, string codes)
    {
        InsertContent(classFile, originCode =>
        {
            if (originCode.Contains(codes))
            {
                throw new Exception("要插入的代码已经存在");
            }
            // 找到最后一个属性
            Regex regex = new Regex(@"public\s+.+\s+\w+\s*{\s*get;\s*set;\s*}");
            Match lastProperty = regex.Matches(originCode).LastOrDefault();
            if (lastProperty != null)
            {
                string lastPropertyString = lastProperty.Value; // "public DbSet<Employee> Employees { get; set; }"
                string modifiedPropertyString = lastPropertyString + Environment.NewLine + _twoTabsSpace + codes;
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
                classLeftBrackets = classLeftBrackets + Environment.NewLine + _twoTabsSpace + codes;

                return regex.Replace(originCode, classLeftBrackets, 1); // 只替换一次
            }
            string newCodeBeforeFirstMethodStatement = codes + firstMethodStatement;
            return originCode.Replace(firstMethodStatement, newCodeBeforeFirstMethodStatement);
        });

    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="classFile"></param>
    /// <param name="methodName"></param>
    /// <param name="codes"></param>
    /// <exception cref="Exception"></exception>
    public static void InsertCodeToMethodTail(string classFile, string methodName, string codes)
    {
        InsertContent(classFile, originCode =>
        {
            if (originCode.Contains(codes))
            {
                throw new Exception("要插入的代码已经存在");
            }
            Regex regex = new(methodName + @"\(.*\r\n\s{8}{[\w\W]*?\s{8}}");
            Match onModelCreatingMatch = regex.Match(originCode);
            string oldPart = onModelCreatingMatch.Value;
            string newPart = onModelCreatingMatch.Value.TrimEnd('}') + _oneTabSpace + codes + Environment.NewLine + _twoTabsSpace + "}";
            return originCode.Replace(oldPart, newPart);
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
                string newPart = onModelCreatingMatch.Value.TrimEnd('}') + _oneTabSpace + codes + Environment.NewLine + _twoTabsSpace + "}";
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
            string newCodes = insertPosition.ToString() + insertPositionTrivia + Environment.NewLine + _oneTabSpace + _twoTabsSpace + codes;
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
    /// 从文件中根据指定的正则表达式搜索出对象集合, 对象的属性为指定正则表达式的分组名
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="targetPattern"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task SearchTxt(string filePath, string targetPattern)
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

        // TODO: 目前时写入到文件中, 考虑根据参数直接返回结果或者写入文件
        string dir = Path.GetDirectoryName(filePath) ?? throw new Exception($"获取josn文件的目录失败");
        var resultFile = Path.Combine(dir, "SearchResult.json");
        await File.WriteAllTextAsync(resultFile, JsonConvert.SerializeObject(searchedResult));
    }
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

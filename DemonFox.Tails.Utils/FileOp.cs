using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace DemonFox.Tails.Utils
{
    public class FileOp
    {        
        public static void DeleteFiles(List<string> files)
        {
            foreach (string file in files)
            {                
                File.Delete(file);                
            }
        }

        public static List<string> FindFilesRecursive(string dirPath, List<string> files = null)
        {
            if (files == null)
            {
                files = new List<string>();
            }
            string[] findedFiles = Directory.GetFiles(dirPath);
            foreach (string file in findedFiles)
            {
                files.Add(file);
            }
            string[] dirs = Directory.GetDirectories(dirPath);
            if (!dirs.Any())
            {
                return files;
            }
            foreach (string dir in dirs)
            {
                if (dir.EndsWith(@"\obj") || dir.EndsWith(@"\bin") || dir.EndsWith(@"\.vs"))
                {
                    continue;
                }
                FindFilesRecursive(dir, files);
            }
            return files;
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
        /// <param name="stopRecursionCondition">files满足一定的条件就停止递归搜索</param>     
        /// <returns></returns>
        public static List<string> FindFilesRecursive(string dir, Expression<Func<string, bool>> filter = null, Func<List<string>, bool> stopRecursionConditionOfFiles = null, List<string> files = null)
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
            if (files == null)
            {
                files = new List<string>();
            }
            // 当前目录下的所有目录
            string[] currentDirs = Directory.GetDirectories(dir);
            string[] currentFiles = Directory.GetFiles(dir);
            files.AddRange(currentFiles.AsQueryable().Where(filter));
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
            Expression<Func<string, bool>> filter = null,
            Func<List<string>, bool> stopRecursionConditionOfDirs = null,
            List<string> dirs = null
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
            if (!currentDirs.Any() || (stopRecursionConditionOfDirs != null && stopRecursionConditionOfDirs(dirs)))
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
            using (StreamReader sr = new StreamReader(file, Encoding.UTF8))
            {
                sr.BaseStream.Seek(0, SeekOrigin.Begin);
                originContent = sr.ReadToEnd();
                sr.Close();
            }

            string newCon = newContentHandler(originContent);

            using (var streamWriter = new StreamWriter(file, false, Encoding.UTF8))
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
            using (StreamReader sr = new StreamReader(settingsFile, Encoding.UTF8))
            {
                string settings = sr.ReadToEnd();
                // 将配置文件内容反序列化为对象
                dynamic settingsObj = JsonConvert.DeserializeObject(settings);
                // 对配置文件内容进行更新操作
                settingsUpdater(settingsObj);

                string updatedSettings = JsonConvert.SerializeObject(settingsObj);
                if (updatedSettings.Length + 20 < UnformatJsonString(settings).Length)
                {
                    throw new Exception("json配置文件的配置减少超过20个字符, 如果您在修改某些配置, 为了防止未知bug引起配置的清空, 建议您手动修改, 否则请修改源代码");
                }
                sr.Close();
                using (StreamWriter sw = new StreamWriter(settingsFile, false, Encoding.UTF8))
                {
                    sw.Write(updatedSettings);
                    sw.Flush();
                    sw.Close();
                }
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
            Regex regex = new Regex(@",\s+");
            json = regex.Replace(json, ",");
            // 去掉"{"后面的空格/缩进
            regex = new Regex(@"{\s+");
            json = regex.Replace(json, "{");
            // 去掉"}"前面的空格/缩进
            regex = new Regex(@"\s+}");
            return regex.Replace(json, "}");
        }

        /// <summary>
        /// 获取json文件的内容并反序列化为对象
        /// </summary>
        /// <param name="settingsFile">json配置文件</param>
        /// <param name="settingsHandler">反序列化前对配置文件的内容进行预处理</param>
        /// <returns></returns>
        public static dynamic SettingsRead(string settingsFile, Func<string, string> settingsHandler = null)
        {
            if (!File.Exists(settingsFile))
            {
                throw new ArgumentException($"配置文件{settingsFile}不存在");
            }
            dynamic settingsObj = null;
            using (StreamReader sr = new StreamReader(settingsFile, Encoding.UTF8))
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
            List<string> properties = new List<string>();
            using (StreamReader sr = new StreamReader(classFile, Encoding.UTF8))
            {
                string fileCon = sr.ReadToEnd();
                Regex reg = new Regex(@"public\s+.+\s+(\w+)\s*{\s*get;\s*set;\s*}");
                MatchCollection matches = reg.Matches(fileCon);

                foreach (Match item in matches)
                {
                    properties.Add(item.Groups[1].Value);
                }
                sr.Close();
            }
            return properties;
        }
        /// <summary>
        /// 读取一个文件的内容
        /// </summary>
        /// <param name="file">目标文件</param>
        /// <returns></returns>
        public static string Read(string file)
        {
            if (!File.Exists(file))
            {
                throw new ArgumentNullException(nameof(file));
            }
            using (StreamReader sr = new StreamReader(file, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// 将内容写入文件
        /// </summary>
        /// <param name="file"></param>
        /// <param name="input"></param>
        public static void Write(string file, string input, bool append, Encoding encoding)
        {
            using (StreamWriter sw = new StreamWriter(file, append, encoding))
            {
                sw.Write(input);
                sw.Flush();
                sw.Close();
            }
        }

        /// <summary>
        /// 判断一个文件中是否已经存在指定内容
        /// </summary>
        /// <param name="file"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static bool IsContentExists(string file, string content)
        {
            using (StreamReader sr = new StreamReader(file, Encoding.UTF8))
            {
                string fileContent = sr.ReadToEnd();
                sr.Close();
                int contentIndex = fileContent.IndexOf(content);
                if (contentIndex == -1)
                {
                    return false;
                }
                return true;
            }
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
            Byte[] buffer = br.ReadBytes(2);
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
    }
}

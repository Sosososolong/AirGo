using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Utils.Constants;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 系统终端命令助手
    /// </summary>
    public class SystemCmd : ICommandExecutor
    {
        /// <summary>
        /// 无参构造函数
        /// </summary>
        public SystemCmd()
        {
            SolutionName = string.Empty;
            UIProjName = string.Empty;
            CoreProjName = string.Empty;
            InfrastructureProjName = string.Empty;
            SolutionFile = string.Empty;
            UIProjFile = string.Empty;
            CoreProjFile = string.Empty;
            InfrastructureProjFile = string.Empty;
        }
        /// <summary>
        /// 终端命令
        /// </summary>
        /// <param name="projectInfo"></param>
        public SystemCmd(ProjectInfo projectInfo)
        {
            SolutionName = projectInfo.SolutionName;
            UIProjName = projectInfo.UIProjName;
            CoreProjName = projectInfo.CoreProjName;
            InfrastructureProjName = projectInfo.InfrastructureProjName;
            SolutionFile = projectInfo.SolutionFile;
            UIProjFile = projectInfo.UIProjFile;
            CoreProjFile = projectInfo.CoreProjFile;
            InfrastructureProjFile = projectInfo.InfrastructureProjFile;
        }
        /// <summary>
        /// 解决方案名称
        /// </summary>
        public string SolutionName { get; set; }
        /// <summary>
        /// MVC层的名字，"BlogDemo.API"
        /// </summary>
        public string UIProjName { get; set; }
        /// <summary>
        /// Core层名字, BlogDemo.Core
        /// </summary>
        public string CoreProjName { get; set; }
        // Infrastructure层的名字, BlogDemo.Infrastructure
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string SolutionFile { get; }
        /// <summary>
        /// 
        /// </summary>
        public string UIProjFile { get; }
        /// <summary>
        /// 
        /// </summary>
        public string CoreProjFile { get; }
        /// <summary>
        /// 
        /// </summary>
        public string InfrastructureProjFile { get; }
        /// <summary>
        /// "dotnet cli" - 创建项目
        /// </summary>
        public string CreateSlnStatement { get { return "dotnet new sln -o " + SolutionName + "; cd " + SolutionName; } }
        /// <summary>
        /// 
        /// </summary>
        public string CreateWebEmptyStatement { get { return "dotnet new web -o " + UIProjName; } }
        /// <summary>
        /// 
        /// </summary>
        public string CreateLibCoreStatement { get { return "dotnet new classlib -o " + CoreProjName; } }
        /// <summary>
        /// 
        /// </summary>
        public string CreateLibInfrastructureStatement { get { return "dotnet new classlib -o " + InfrastructureProjName; } }
        /// <summary>
        /// "dotnet cli" - 将项目添加到解决方案中
        /// </summary>
        public string AddinSlnAPI => $"dotnet sln {SolutionFile} add ./{UIProjName}/{UIProjFile}";  // dotnet sln BlogDemo.sln add .\BlogDemo.API\BlogDemo.API.csproj
        /// <summary>
        /// 添加解决方案
        /// </summary>
        public string AddinSlnCore => $"dotnet sln {SolutionFile} add ./{CoreProjName}/{CoreProjFile}";
        /// <summary>
        /// 添加解决方案
        /// </summary>
        public string AddinSlnInfrastructure => $"dotnet sln {SolutionFile} add ./{InfrastructureProjName}/{InfrastructureProjFile}";
        /// <summary>
        /// CPU使用率命令-Linux
        /// </summary>
        public const string CPURateCommandLinux = "top -b -n1 | grep \"Cpu(s)\" | awk '{print $2 + $4}'";
        /// <summary>
        /// CPU使用率命令-Windows
        /// </summary>
        public const string CPURateCommandWindows = "wmic cpu get LoadPercentage";
        static Regex WindowsPathPattern = new(@"^[A-Z]:\\([^*\^""\<\>\?\|:]+)*>.*");
        /// <summary>
        /// 本地执行脚本
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<CommandResult> ExecuteAsync(string command)
        {
            var outputs = ExecuteSingleCommandAsync(command);
            await foreach (var output in outputs)
            {
                var cmdRes = output.StartsWith("[ERR]") ? new CommandResult(false, output) : new CommandResult(true, output);
                yield return cmdRes;
            }
            yield break;
        }
        /// <summary>
        /// 本地执行脚本
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public static async Task<List<string>> ExecuteAsync(params string[] commands)
        {
            // TODO: 修改为配置
            string winShell = "powershell.exe";
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(currentDir, $"TEMP_SYSTEMCMD_{DateTime.Now:yyyyMMddHHmmssFFFFFFF}");
            var dirInfo = Directory.CreateDirectory(tempDir);
            var startInfo = new ProcessStartInfo
            {
                // 设置要启动的应用程序
                FileName = Directory.Exists("C:/") ? winShell : "/bin/bash",
                // 是否使用操作系统shell启动
                UseShellExecute = false,
                // 接受来自调用程序的输入信息
                RedirectStandardInput = true,

                // 重定向输出, 即时输出已经全部重定向写入文件, 后面依然需要读取程序全部输出, 只有读取全部输出完毕代表脚本全部执行完毕, 才能继续下一步操作读取文件中的输出内容
                RedirectStandardOutput = true,
                // 输出错误
                RedirectStandardError = true,
                // 不显示程序窗口
                CreateNoWindow = true
            };

            Process p = new() { StartInfo = startInfo };
            // 启动程序
            p.Start();

            int commandIndex = -1;
            // 向cmd窗口发送输入信息
            foreach (string cmdItem in commands)
            {
                commandIndex++;
                // 执行一个cmd命令
                string logFileName = $"cmd{commandIndex}";
                string outputFile = Path.Combine(tempDir, $"{logFileName}.log");
                string tempScriptFile = Path.Combine(tempDir, $"{logFileName}.ps1");
                string scripts = $$"""
                    chcp 65001
                    try {
                    {{string.Join('\n', cmdItem.Split('\n').Select(x => $"{SpaceConstants.OneTabSpaces}"))}} *> {{outputFile}}
                     } catch {
                        # 捕获到错误后，将错误信息写入文件  
                        $_.Exception.Message | Out-File -FilePath "{{outputFile}}" -Append
                    }
                    """;
                File.WriteAllText(tempScriptFile, scripts, Encoding.UTF8);
                await p.StandardInput.WriteLineAsync($"PowerShell.exe -File {tempScriptFile} -ExecutionPolicy Bypass");
            }

            p.StandardInput.AutoFlush = true;
            p.StandardInput.Close();

            // 获取输出信息, 读取完说明脚本已经执行完毕, 可以从文件中读取输出信息
            string successOutput = await p.StandardOutput.ReadToEndAsync();
            string errorOutput = await p.StandardError.ReadToEndAsync();

            List<string> outputList = [];
            var tempFiles = Directory.GetFiles(tempDir).OrderBy(f => f);// (File.GetCreationTime).ThenBy
            foreach (var f in tempFiles)
            {
                if (f.EndsWith(".log"))
                {
                    outputList.Add(File.ReadAllText(f));
                }
            }

            // 保留最近的10个临时文件夹，其他删除
            var tempDirs = Directory.GetDirectories(currentDir, "TEMP_SYSTEMCMD_*")
                                    .OrderByDescending(d => d)
                                    .Skip(10);
            foreach (var dir in tempDirs)
            {
                Directory.Delete(dir, true);
            }

            return outputList;
        }
        /// <summary>
        /// 执行一个终端命令
        /// </summary>
        /// <param name="cmdTxt"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<string> ExecuteSingleCommandAsync(string cmdTxt)
        {
            // TODO: 修改为配置
            string winShell = "powershell.exe";
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(currentDir, $"TEMP_SYSTEMCMD_{DateTime.Now:yyyyMMddHHmmssFFFFFFF}");
            _ = Directory.CreateDirectory(tempDir);
            var startInfo = new ProcessStartInfo
            {
                // 设置要启动的应用程序
                FileName = Directory.Exists("C:/") ? winShell : "/bin/bash",
                // 是否使用操作系统shell启动
                UseShellExecute = false,
                // 接受来自调用程序的输入信息
                RedirectStandardInput = true,

                // 重定向输出, 即时输出已经全部重定向写入文件, 后面依然需要读取程序全部输出, 只有读取全部输出完毕代表脚本全部执行完毕, 才能继续下一步操作读取文件中的输出内容
                RedirectStandardOutput = true,
                // 输出错误
                RedirectStandardError = true,
                // 不显示程序窗口
                CreateNoWindow = true
            };

            Process p = new() { StartInfo = startInfo };
            // 启动程序
            p.Start();

            // 向cmd窗口发送输入信息, 执行一个cmd命令
            string logFileName = $"cmd";
            string outputFile = Path.Combine(tempDir, $"{logFileName}.log");
            string tempScriptFile = Path.Combine(tempDir, $"{logFileName}.ps1");
            string scripts = $$"""
                    try {
                        {{cmdTxt}}
                     } catch {
                        # 捕获到错误后，将错误信息写入文件  
                        $_.Exception.Message | Out-File -FilePath "{{outputFile}}" -Append
                    }
                    """;
            File.WriteAllText(tempScriptFile, scripts, Encoding.UTF8);
            string inputContent = $"PowerShell.exe -File {tempScriptFile} -ExecutionPolicy Bypass";
            await p.StandardInput.WriteLineAsync(inputContent);

            p.StandardInput.AutoFlush = true;
            p.StandardInput.Close();

            // 获取输出信息
            bool validStartOutput = false;
            while (!p.StandardOutput.EndOfStream)
            {
                string line = await p.StandardOutput.ReadLineAsync();
                if (line.Contains(inputContent) && !validStartOutput)
                {
                    validStartOutput = true;
                }
                if (validStartOutput && !line.Contains(inputContent))
                {
                    yield return line;
                }
            }
            if (File.Exists(outputFile))
            {
                yield return File.ReadAllText(outputFile);
            }
        }
        /// <summary>
        /// 本地执行脚本
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public static async Task<List<string>> ExecuteParallellyAsync(params string[] commands)
        {
            // TODO: 修改为配置
            string winShell = "powershell.exe";
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(currentDir, $"TEMP_SYSTEMCMD_{DateTime.Now:yyyyMMddHHmmssFFFFFFF}");
            var dirInfo = Directory.CreateDirectory(tempDir);
            //string winShell = "D:/Program Files/Git/bin/bash.exe";
            var startInfo = new ProcessStartInfo
            {
                // 设置要启动的应用程序
                FileName = Directory.Exists("C:/") ? winShell : "/bin/bash",
                // 是否使用操作系统shell启动
                UseShellExecute = false,
                // 接受来自调用程序的输入信息
                RedirectStandardInput = true,

                // 重定向输出, 即时输出已经全部重定向写入文件, 后面依然需要读取程序全部输出, 只有读取全部输出完毕代表脚本全部执行完毕, 才能继续下一步操作读取文件中的输出内容
                RedirectStandardOutput = true,
                // 输出错误
                RedirectStandardError = true,
                // 不显示程序窗口
                CreateNoWindow = true
            };

            List<Task> executeTasks = [];
            ConcurrentBag<(int, string)> outputs = [];
            int commandIndex = -1;
            foreach (var cmdItem in commands)
            {
                commandIndex++;

                var executeTask = RunProcess(startInfo, cmdItem);

                executeTasks.Add(executeTask);
            }

            await Task.WhenAll(executeTasks);
            var outputList = outputs.OrderBy(x => x.Item1).Select(x => x.Item2).ToList();
            return outputList;

            async Task RunProcess(ProcessStartInfo startInfo, string cmdItem)
            {
                using Process p = new() { StartInfo = startInfo };
                // 启动程序
                p.Start();

                // 向cmd窗口发送输入信息, 执行一个cmd命令
                await p.StandardInput.WriteLineAsync(cmdItem);

                p.StandardInput.AutoFlush = true;
                p.StandardInput.Close();

                // 获取输出信息, 读取完说明脚本已经执行完毕, 可以从文件中读取输出信息
                string successOutput = await p.StandardOutput.ReadToEndAsync();
                string errorOutput = await p.StandardError.ReadToEndAsync();

                var output = (successOutput + Environment.NewLine + Environment.NewLine + errorOutput).Trim();

                // 获取输出信息, 不适用于多条命令混合在一起, 因为只能一次性读取输出, 然后一次性读取错误(不知道是哪条命令的错误)
                // 删除powershell.exe本身的输出显示文字

                var outputList = output.Split(cmdItem).ToList();
                if (outputList.Count > 1)
                {
                    outputList.RemoveAt(0);
                    // 剩下输出的所有行, 还需要删除最后一行的用户输入行, 类似于: "PS C:\Users\UserName>"
                    var otherLines = string.Join("", outputList).Split(Environment.NewLine).ToList();
                    var lastLine = otherLines.Last();
                    if (lastLine.StartsWith("PS "))
                    {
                        otherLines.Remove(lastLine);
                    }
                    output = string.Join(Environment.NewLine, otherLines).Trim();
                }
                //获取输出信息, 不适用于多条命令混合在一起, 因为只能一次性读取输出, 然后一次性读取错误(不知道是哪条命令的错误)
                outputs.Add((commandIndex, output));
            };
        }

        /// <summary>
        /// 获取进程的CPU使用率和占用内存
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static async Task<List<string>> GetProcessCpuAndRamAsync(string processName)
        {
            // 使用Process类实现
            var processes = Process.GetProcessesByName(processName);
            ConcurrentBag<string> result = [];
            Task[] tasks = processes.Select(async p =>
            {
                await GetProcessCpuAverageRate(p, result);
            }).ToArray();
            await Task.WhenAll(tasks);
            return [.. result];

            static async Task GetProcessCpuAverageRate(Process p, ConcurrentBag<string> result)
            {
                int interval = 1000;
                //上次记录的CPU时间
                var prevCpuTime = TimeSpan.Zero;
                for (int i = 0; i < 2; i++)
                {
                    //当前时间
                    var curTime = p.TotalProcessorTime;
                    //间隔时间内的CPU运行时间除以逻辑CPU数量
                    var value = (curTime - prevCpuTime).TotalMilliseconds / interval / Environment.ProcessorCount * 100;
                    prevCpuTime = curTime;
                    await Task.Delay(interval);
                    if (i > 0)
                    {
                        result.Add($"{p.ProcessName} {p.Id}:{Math.Round(value, 1)} {GetProcessRam(p)}");
                    }
                }
            }
        }

        #region 主机信息管理
        /// <summary>
        /// CPU数量
        /// </summary>
        public static int CpuCount => Environment.ProcessorCount;
        /// <summary>
        /// 计算机名称
        /// </summary>
        public static string MachineName => Environment.MachineName;
        /// <summary>
        /// 系统名称
        /// </summary>
        public static string OSName => RuntimeInformation.OSDescription;
        /// <summary>
        /// 系统架构
        /// </summary>
        public static string OSArchitecture => RuntimeInformation.OSArchitecture.ToString();

        /// <summary>
        /// .Net版本名称
        /// </summary>
        public static string DoNetName => RuntimeInformation.FrameworkDescription;

        /// <summary>
        /// 服务开始运行时间
        /// </summary>
        public static string AppStartTime => Process.GetCurrentProcess().StartTime.ToString("G");

        /// <summary>
        /// 服务运行时间
        /// </summary>
        public static string AppRunTime => DateTimeHelper.FormatSeconds((DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds);

        /// <summary>
        /// IP地址
        /// </summary>
        public static List<string> IpList
        {
            get
            {
                List<string> strIp = [];

                //NetworkInterface：提供网络接口的配置和统计信息。
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface adapter in adapters)
                {
                    IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                    UnicastIPAddressInformationCollection allAddress = adapterProperties.UnicastAddresses;

                    //这里是根据网络适配器名称找到对应的网络，adapter.Name即网络适配器的名称
                    if (allAddress.Count > 0 && adapter.Name == "WLAN")
                    {
                        foreach (UnicastIPAddressInformation addr in allAddress)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                strIp.Add(addr.Address.ToString());
                            }
                        }
                    }
                }

                if (strIp.Count == 0)
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            strIp.Add(ip.ToString());
                        }
                    }
                }
                return strIp;
            }
        }

        /// <summary>
        /// 是否Unix系统
        /// </summary>
        /// <returns></returns>
        public static bool IsUnix => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        /// <summary>
        /// 主机磁盘信息
        /// </summary>
        public static async Task<List<DiskInfo>> GetDiskInfosAsync()
        {
            List<DiskInfo> diskInfos = [];

            if (IsUnix)
            {
                try
                {
                    List<string> outputs = await ExecuteAsync("df -m / | awk '{print $2,$3,$4,$5,$6}'");
                    string output = outputs.FirstOrDefault();
                    var arr = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (arr.Length == 0)
                        return diskInfos;

                    var rootDisk = arr[1].Split(' ', (char)StringSplitOptions.RemoveEmptyEntries);
                    if (rootDisk == null || rootDisk.Length == 0)
                    {
                        return diskInfos;
                    }
                    DiskInfo diskInfo = new()
                    {
                        DiskName = "/",
                        TotalSize = long.Parse(rootDisk[0]) / 1024,
                        Used = long.Parse(rootDisk[1]) / 1024,
                        AvailableFreeSpace = long.Parse(rootDisk[2]) / 1024,
                        AvailablePercent = decimal.Parse(rootDisk[3].Replace("%", ""))
                    };
                    diskInfos.Add(diskInfo);
                }
                catch { }
            }
            else
            {
                var driv = DriveInfo.GetDrives();
                foreach (var item in driv)
                {
                    try
                    {
                        var obj = new DiskInfo()
                        {
                            DiskName = item.Name,
                            TypeName = item.DriveType.ToString(),
                            TotalSize = item.TotalSize / 1024 / 1024 / 1024,
                            AvailableFreeSpace = item.AvailableFreeSpace / 1024 / 1024 / 1024,
                        };
                        obj.Used = obj.TotalSize - obj.AvailableFreeSpace;
                        obj.AvailablePercent = decimal.Ceiling(obj.Used / (decimal)obj.TotalSize * 100);
                        diskInfos.Add(obj);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return diskInfos;
        }
        /// <summary>
        /// 主机内存信息
        /// </summary>
        public static async Task<MemoryInfo> GetMemoryInfoAsync()
        {
            MemoryInfo memoryInfo = new();
            if (IsUnix)
            {
                try
                {
                    string output = (await ExecuteAsync("free -m | awk '{print $2,$3,$4,$5,$6}'")).FirstOrDefault();
                    var lines = output.Split('\n', (char)StringSplitOptions.RemoveEmptyEntries);
                    if (lines is null || lines.Length == 0)
                    {
                        return memoryInfo;
                    }

                    var memory = lines[1].Split(' ', (char)StringSplitOptions.RemoveEmptyEntries);
                    if (memory.Length >= 3)
                    {
                        memoryInfo.Total = double.Parse(memory[0]);
                        memoryInfo.Used = double.Parse(memory[1]);
                        memoryInfo.Free = double.Parse(memory[2]);//m
                    }
                    memoryInfo.Total = double.Parse(lines[1]);
                    memoryInfo.Used = double.Parse(lines[2]);
                    memoryInfo.Free = double.Parse(lines[3]);
                    memoryInfo.UsedRam = memoryInfo.Used.ToString("N2") + " MB";
                    var cpuRateInfo = (await ExecuteAsync(CPURateCommandLinux)).FirstOrDefault();
                    memoryInfo.CpuRate = Math.Ceiling(double.Parse(cpuRateInfo));
                }
                catch
                {
                }
            }
            else
            {
                try
                {
                    var output = (await ExecuteAsync("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value")).FirstOrDefault();

                    var freeMemory = Regex.Match(output, @"FreePhysicalMemory\s*=\s*(\d+)").Groups[1].Value; // lines[0].Split('=', (char)StringSplitOptions.RemoveEmptyEntries);
                    var totalMemory = Regex.Match(output, @"TotalVisibleMemorySize\s*=\s*(\d+)").Groups[1].Value;

                    memoryInfo.Total = Math.Round(double.Parse(totalMemory) / 1024, 0);
                    memoryInfo.Free = Math.Round(double.Parse(freeMemory) / 1024, 0);
                    memoryInfo.Used = memoryInfo.Total - memoryInfo.Free;
                    var cpuRateInfo = (await ExecuteAsync(CPURateCommandWindows)).FirstOrDefault().Trim();
                    var cpuRateValue = cpuRateInfo.Replace("LoadPercentage", string.Empty).Trim();
                    memoryInfo.CpuRate = double.Parse(cpuRateValue);
                }
                catch
                {
                }
            }
            return memoryInfo;
        }
        /// <summary>
        /// 获取进程的内存使用 MB
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double GetProcessRam(Process p) => Math.Round((double)p.WorkingSet64 / 1024 / 1024, 0);
        /// <summary>
        /// 获取系统状态
        /// </summary>
        /// <returns></returns>
        public static async Task<ServerInfo> GetServerAndAppInfoAsync()
        {
            var memoryInfo = await GetMemoryInfoAsync();
            return new()
            {
                MachineName = MachineName,
                OSName = OSName,
                OSArchitecture = OSArchitecture,
                DoNetName = DoNetName,
                IP = string.Join("; ", IpList),
                CpuCount = CpuCount,
                DiskInfos = await GetDiskInfosAsync(),
                MemoryInfo = memoryInfo,
                AppStartTime = AppStartTime,
                AppRunTime = AppRunTime,
                AppRam = Math.Round((double)Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024, 0),
                AppRamRate = Math.Round((double)Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024 / memoryInfo.Total, 4),
            };
        }
        #endregion
    }
    /// <summary>
    /// 主机信息
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// 主机名称
        /// </summary>
        public string MachineName { get; set; } = string.Empty;
        /// <summary>
        /// 操作系统名称
        /// </summary>
        public string OSName { get; set; } = string.Empty;
        /// <summary>
        /// 操作系统架构
        /// </summary>
        public string OSArchitecture { get; set; } = string.Empty;
        /// <summary>
        /// dotnet版本
        /// </summary>
        public string DoNetName { get; set; } = string.Empty;
        /// <summary>
        /// Ip地址
        /// </summary>
        public string IP { get; set; } = string.Empty;
        /// <summary>
        /// Cpu核心数量
        /// </summary>
        public int CpuCount { get; set; }
        /// <summary>
        /// 磁盘信息
        /// </summary>
        public List<DiskInfo> DiskInfos { get; set; } = [];
        /// <summary>
        /// 内存信息
        /// </summary>
        public MemoryInfo MemoryInfo { get; set; } = new MemoryInfo();

        /// <summary>
        /// 应用启动时间
        /// </summary>
        public string AppStartTime { get; set; } = string.Empty;
        /// <summary>
        /// 应用运行时间
        /// </summary>
        public string AppRunTime { get; set; } = string.Empty;
        /// <summary>
        /// APP占用内存
        /// </summary>
        public double AppRam { get; set; }
        /// <summary>
        /// APP内存使用率
        /// </summary>
        public double AppRamRate { get; set; }
    }
    /// <summary>
    /// 内存信息
    /// </summary>
    public class MemoryInfo
    {

        /// <summary>
        /// 总内存 MB
        /// </summary>
        public double Total { get; set; }
        /// <summary>
        /// 已使用内存 MB
        /// </summary>
        public double Used { get; set; }
        /// <summary>
        /// 剩余可用内存 MB
        /// </summary>
        public double Free { get; set; }
        /// <summary>
        /// 已使用内存 MB
        /// </summary>
        public string UsedRam { get; set; } = string.Empty;
        /// <summary>
        /// CPU使用率%
        /// </summary>
        public double CpuRate { get; set; }
        /// <summary>
        /// 总内存 GB
        /// </summary>
        public double TotalRAM { get; set; }
        /// <summary>
        /// 内存使用率 %
        /// </summary>
        public double RAMRate { get; set; }
        /// <summary>
        /// 空闲内存
        /// </summary>
        public string FreeRam { get; set; } = string.Empty;
    }
    /// <summary>
    /// 磁盘信息
    /// </summary>
    public class DiskInfo
    {
        /// <summary>
        /// 磁盘的名称。
        /// </summary>
        public string DiskName { get; set; } = string.Empty;

        /// <summary>
        /// 磁盘的类型。
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 磁盘的剩余空间总量（字节）。
        /// </summary>
        public long TotalFree { get; set; }

        /// <summary>
        /// 磁盘的总大小（字节）。
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// 磁盘已使用的空间量（字节）。
        /// </summary>
        public long Used { get; set; }

        /// <summary>
        /// 磁盘可使用的剩余空间（字节）。
        /// </summary>
        public long AvailableFreeSpace { get; set; }

        /// <summary>
        /// 磁盘剩余空间的百分比。
        /// </summary>
        public decimal AvailablePercent { get; set; }
    }
}

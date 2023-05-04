using Microsoft.Extensions.Hosting;
using Renci.SshNet;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.App.Utils;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// 每一个实例对应一台远程主机的SSH连接
    /// </summary>
    public partial class SshHelper : IDisposable
    {
        private readonly int _maxConnections;
        private int _currentConnections;
        private readonly object _syncLock = new();
        private readonly static ConcurrentBag<SshClient> _sshConnectionPool = new();
        private readonly static ConcurrentBag<SftpClient> _sftpConnectionPool = new();
        private string Host { get; set; }
        private string UserName { get; set; }
        private string PrivateKey { get; set; }
        public SshClient GetConnection()
        {
            if (!_sshConnectionPool.TryTake(out SshClient? connection))
            {
                lock (_syncLock)
                {
                    if (_currentConnections < _maxConnections)
                    {
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了SshClient");
                        connection = new SshClient(Host, UserName, new PrivateKeyFile(PrivateKey));
                        connection.Connect();
                        _sshConnectionPool.Add(connection);
                        _currentConnections++;
                    }
                    else
                    {
                        throw new InvalidOperationException("Reached maximum number of connections.");
                    }
                }
            }
            else
            {
                if (!connection.IsConnected)
                {
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 检测到连接池中的连接异常断开, 重新连接xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                    connection.Connect();
                }
            }
            if (connection == null)
            {
                _sshConnectionPool.TryTake(out connection);
            }

            return connection ?? throw new Exception("Ssh connection is null");
        }
        public SftpClient GetSftpConnection()
        {
            if (!_sftpConnectionPool.TryTake(out SftpClient? connection))
            {
                lock (_syncLock)
                {
                    if (_currentConnections < _maxConnections)
                    {
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了SFtpClient");
                        connection = new SftpClient(Host, UserName, new PrivateKeyFile(PrivateKey));
                        connection.Connect();
                        _sftpConnectionPool.Add(connection);
                        _currentConnections++;
                    }
                    else
                    {
                        throw new InvalidOperationException("Reached maximum number of connections.");
                    }
                }
            }
            if (connection == null)
            {
                _sftpConnectionPool.TryTake(out connection);
            }

            return connection ?? throw new Exception("Sftp connection is null");
        }
        /// <summary>
        /// 连接返回连接池
        /// </summary>
        /// <param name="connection"></param>
        private static void ReturnConnection(SshClient connection)
        {
            if (connection != null)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 执行完RunCommand返回连接池");
                _sshConnectionPool.Add(connection);
            }
        }
        /// <summary>
        /// 连接返回连接池
        /// </summary>
        /// <param name="connection"></param>
        private static void ReturnConnection(SftpClient connection)
        {
            if (connection != null)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 执行完毕SFTP返回连接池");
                _sftpConnectionPool.Add(connection);
            }
        }


        public void Dispose()
        {
            while (_sshConnectionPool.TryTake(out SshClient connection))
            {
                connection.Disconnect();
                connection.Dispose();
                _currentConnections--;
            }
        }
        public SshHelper(string host, string username, string privateKey)
        {
            Console.WriteLine($"P{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了一个SshHelper对象");
            Host = host;
            UserName = username;
            PrivateKey = privateKey;

            _maxConnections = 20;
            _currentConnections = 0;
        }
        /// <summary>
        /// 释放资源
        /// </summary>
        ~SshHelper()
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 关闭释放所有Client");
        }


        /// <summary>
        /// 运行命令
        /// shell命令
        /// 上传 upload   (?<local>[^\s]+) (?<remote>[^\s]+) -include=(?<include>[^\s+]) -exclude=(?<exclude>[^\s]+) 
        /// 下载 download (?<local>[^\s]+) (?<remote>[^\s]+) -include=(?<include>[^\s+]) -exclude=(?<exclude>[^\s]+) 
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public SshCommand RunCommand(string command)
        {
            var cmdMatch = RegexConst.CommandRegex().Match(command);
            var action = cmdMatch.Groups["action"].Value.Replace('\\', '/');
            var local = cmdMatch.Groups["local"].Value.Replace('\\', '/');
            var remote = cmdMatch.Groups["remote"].Value.Replace('\\', '/');
            var includes = cmdMatch.Groups["include"].Value.Replace('\\', '/')?.Split(',');
            var excludes = cmdMatch.Groups["exclude"].Value.Replace('\\', '/')?.Split(',');
            if (action.ToLower() == "upload")
            {
                var conn = GetSftpConnection();
                if (string.IsNullOrWhiteSpace(local) && string.IsNullOrWhiteSpace(remote))
                {
                    throw new Exception("上传文件格式错误: " + command);
                }

                var localFiles = new List<string>();
                // 上传目录
                if (Directory.Exists(local))
                {
                    #region 上传目录
                    // 统一目录格式
                    local = local.TrimEnd('.');
                    remote = remote.TrimEnd('.');
                    local = local.EndsWith('/') ? local : $"{local}/";
                    remote = remote.EndsWith('/') ? remote : $"{remote}/";

                    if (!conn.Exists(remote))
                    {
                        var ssh = GetConnection();
                        ssh.RunCommand($"mkdir -p {remote}");
                        ReturnConnection(ssh);
                    }
                    localFiles = FileHelper.FindFilesRecursive(local, f => includes == null || (includes.Any(part => Path.GetFileName(f).Contains(part)) && !excludes.Any(part => Path.GetFileName(f).Contains(part))), null, null);
                    foreach (var localFile in localFiles)
                    {
                        var localFileRelativePath = localFile.Replace(local, "");
                        var targetFilePath = Path.Combine(remote, localFileRelativePath);
                        using var fs = File.OpenRead(localFile);
                        conn.UploadFile(fs, targetFilePath);
                    }
                    #endregion
                }
                else if (File.Exists(local))
                {
                    #region 上传文件
                    using var fs = File.OpenRead(local);
                    if (conn.Exists(remote))
                    {
                        var remoteAttributes = conn.GetAttributes(remote);
                        if (remoteAttributes.IsDirectory)
                        {
                            remote = Path.Combine(remote, local.Split('/').Last()).Replace('\\', '/');
                        }
                    }
                    else
                    {
                        var ssh = GetConnection();
                        ssh.RunCommand($"mkdir -p {remote}");
                        ReturnConnection(ssh);
                        remote = Path.Combine(remote, local.Split('/').Last()).Replace('\\', '/');
                    }
                    conn.UploadFile(fs, remote);
                    localFiles.Add(local);
                    #endregion
                }
                else
                {
                    throw new Exception("上传文件格式错误: " + command);
                }
                ReturnConnection(conn);
                return null;
            }
            else if (action.ToLower() == "download")
            {
                var conn = GetSftpConnection();
                if (string.IsNullOrWhiteSpace(local) && string.IsNullOrWhiteSpace(remote))
                {
                    throw new Exception("上传文件格式错误: " + command);
                }
                if (!conn.Exists(remote))
                {
                    throw new Exception($"远程下载: [{remote}]不存在");
                }

                var remoteAttributes = conn.GetAttributes(remote);
                if (remoteAttributes.IsDirectory)
                {
                    #region 下载目录
                    Console.WriteLine($"下载目录: {remote}");
                    // 下载目录
                    // 统一目录格式
                    local = local.TrimEnd('.');
                    remote = remote.TrimEnd('.');
                    local = local.EndsWith('/') ? local : $"{local}/";
                    remote = remote.EndsWith('/') ? remote : $"{remote}/";

                    // 获取远程目录下所有文件
                    var remoteFiles = GetRemoteFiles(remote, includes, excludes);

                    // 一个文件一个文件地下载
                    foreach (var remoteFile in remoteFiles)
                    {
                        var filename = Path.GetFileName(remoteFile);
                        var relativePath = remoteFile.Replace(remote, string.Empty); // "www/index.html"
                        var localfile = Path.Combine(local, relativePath).Replace('\\', '/');
                        
                        // 校验本地目录
                        var localfileDir = Path.GetDirectoryName(localfile)?.Replace('\\', '/');
                        if (!string.IsNullOrWhiteSpace(localfileDir) && !Directory.Exists(localfileDir))
                        {
                            Directory.CreateDirectory(localfileDir);
                        }
                        
                        using var localFile = File.OpenWrite(localfile);
                        conn.DownloadFile(remoteFile, localFile);
                    }
                    #endregion
                }
                else
                {
                    // 下载文件
                    Console.WriteLine($"下载文件: {local}");
                    var fileDir = Path.GetDirectoryName(local);
                    var file = local;
                    if (local.EndsWith('/'))
                    {
                        fileDir = local;
                        file = Path.Combine(local, Path.GetFileName(remote));
                    }
                    if (!Directory.Exists(fileDir) && !string.IsNullOrWhiteSpace(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    using var localFile = File.OpenWrite(local);
                    conn.DownloadFile(remote, localFile);
                }
                ReturnConnection(conn);
                return null;
            }
            else
            {
                var conn = GetConnection();
                var sshCommand = conn.RunCommand(command);
                ReturnConnection(conn);
                return sshCommand;
            }
        }
        public string[] GetRemoteFiles(string remotePath, string[]? includes, string[]? excludes)
        {
            #region 生成正则
            string includePattern = string.Empty;
            if (includes is not null && includes.Any())
            {
                foreach (var include in includes)
                {
                    includePattern += $"{include}\\|";
                }
                includePattern.TrimEnd('|').TrimEnd('\\');
            }
            if (!string.IsNullOrWhiteSpace(includePattern))
            {
                includePattern = $"-iregex '.*\\({includePattern}\\).*'";
            }
            string excludePattern = string.Empty;
            if (excludes is not null && excludes.Any())
            {
                foreach (var exclude in excludes)
                {
                    excludePattern += $"{exclude}\\|";
                }
                excludePattern.TrimEnd('|').TrimEnd('\\');
            }
            if (!string.IsNullOrWhiteSpace(excludePattern))
            {
                excludePattern = $"-iregex '.*\\({excludePattern}\\).*'";
            }
            #endregion

            var ssh = GetConnection();
            var filesResult = ssh.RunCommand($"find {remotePath} -type f {includePattern} {excludePattern} -maxdepth 4 | head -n 100000").Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ReturnConnection(ssh);
            return filesResult;
        }
        /// <summary>
        /// 使用ssh连接主机执行命令
        /// </summary>
        /// <param name="host"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="command"></param>
        public Tuple<bool, string> ExecuteCommand(string command)
        {
            var conn = GetConnection();
            var cmd = conn.CreateCommand(command);
            _ = cmd.Execute();
            var output = cmd.Result;
            var error = cmd.Error;

            ReturnConnection(conn);
            
            if (string.IsNullOrWhiteSpace(error))
            {
                return Tuple.Create(true, output);
            }
            return Tuple.Create(false, error);
        }
        
        /// <summary>
        /// 使用SSH连接主机处理文件, 服务器上没有该文件则上传; 如果文件上传了, 说明不是容器环境那么也需要将处理后的文件下载下来
        /// </summary>
        /// <param name="host">远程主机, 如192.168.1.229</param>
        /// <param name="localPathSourceFile">文件在form环境中的路径, 如: C:/temp/temp.doc</param>
        /// <param name="remotePathSourceFile">文件上传到服务器上的路径, 如: /home/administrator/web/attachments/Convert/temp.doc</param>
        /// <param name="localPathResultFile">处理后的文件在form环境中的路径, 如: C:/temp/temp.pdf</param>
        /// <param name="remotePathResultFile">处理后的文件在服务器上的路径, 如: /home/administrator/web/attachments/Convert/temp.pdf</param>
        /// <param name="username">服务器用户名</param>
        /// <param name="privateKey">本地/容器的ssh密钥</param>
        /// <param name="command">处理文件的命令</param>
        public Tuple<bool, string> HandleFile(string localPathSourceFile, string remotePathSourceFile, string localPathResultFile, string remotePathResultFile, string command)
        {
            remotePathSourceFile = remotePathSourceFile.Replace("\\", "/");

            remotePathResultFile = remotePathResultFile.Replace("\\", "/");

            command = command.Replace("\\", "/");

            // 如果是容器环境, 远程主机是宿主机, 文件可能已经已经挂载到宿主机, 不需要上传
            var sftp = GetSftpConnection();
            var client = GetConnection();
            var fileRemoteExist = sftp.Exists(remotePathSourceFile);

            bool fileUploaded = false;
            if (!fileRemoteExist)
            {
                var remoteDir = Path.GetDirectoryName(remotePathSourceFile).Replace('\\', '/');
                if (!sftp.Exists(remoteDir))
                {

                    client.RunCommand($"mkdir -p {remoteDir}");
                }
                // 上传文件
                var fileLocalStream = new MemoryStream(File.ReadAllBytes(localPathSourceFile));
                sftp.UploadFile(fileLocalStream, remotePathSourceFile);

                fileUploaded = true;
            }
            var cmd = client.CreateCommand(command);
            _ = cmd.Execute();
            var output = cmd.Result;
            var error = cmd.Error;

            if (fileUploaded)
            {
                using var downloadedFile = File.OpenWrite(localPathResultFile);
                sftp.DownloadFile(remotePathResultFile, downloadedFile);
                sftp.Delete(remotePathSourceFile);
                sftp.Delete(remotePathResultFile);
            }

            ReturnConnection(client);
            ReturnConnection(sftp);

            if (string.IsNullOrWhiteSpace(error))
            {
                return Tuple.Create(true, output);
            }
            return Tuple.Create(false, error);
        }
    }
}

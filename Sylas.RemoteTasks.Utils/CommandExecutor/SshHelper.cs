using Renci.SshNet;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 每一个实例对应一台远程主机的SSH连接
    /// </summary>
    public class SshHelper : IDisposable, ICommandExecutor
    {
        private readonly int _maxConnections;
        private int _currentConnections;
        private readonly object _syncLock = new();
        private readonly List<SshClient> _sshConnectionPool = [];
        private readonly List<SftpClient> _sftpConnectionPool = [];
        private string Host { get; set; }
        private int Port { get; } = 0;
        private string UserName { get; set; }
        private readonly IPrivateKeySource[] _privateKeyFiles;
        static SemaphoreSlim _semaphoneSlimLock = new(1, 1);
        /// <summary>
        /// 从池中获取一个SSH连接
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        private async Task<SshClient> GetConnectionAsync()
        {
            if (!_sshConnectionPool.TryTake(out SshClient? connection))
            {
                //lock (_syncLock)
                //{
                //    if (_currentConnections < _maxConnections)
                //    {
                //        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了SshClient");
                //        connection = new SshClient(Host, Port, UserName, _privateKeyFiles);
                //        await connection.ConnectAsync();
                //        // 创建时不需要添加到池中, 用完返回进池就可以了
                //        //_sshConnectionPool.Add(connection);
                //        _currentConnections++;
                //    }
                //    else
                //    {
                //        throw new InvalidOperationException("Reached maximum number of connections.");
                //    }
                //}

                await _semaphoneSlimLock.WaitAsync();
                // finally保证异常时也会-1
                try
                {
                    if (_currentConnections < _maxConnections)
                    {
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了SshClient");
                        connection = new SshClient(Host, Port, UserName, _privateKeyFiles);
                        
                        CancellationTokenSource cts = new();
                        await connection.ConnectAsync(cts.Token);
                        
                        // 创建时不需要添加到池中, 用完返回进池就可以了
                        //_sshConnectionPool.Add(connection);
                        _currentConnections++;
                    }
                    else
                    {
                        throw new InvalidOperationException("Reached maximum number of connections.");
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    _semaphoneSlimLock.Release();
                }
            }

            if (connection is null)
            {
                throw new Exception($"从连接池获取的SshClient对象为空, Host: {Host}");
            }
            if (!connection.IsConnected)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 检测到连接池中的SSH连接异常断开, 重新连接xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                connection.Connect();
            }
            return connection ?? throw new Exception("Ssh connection is null");
        }
        /// <summary>
        /// 从池中获取一个SFTP连接
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public SftpClient GetSftpConnection()
        {
            if (!_sftpConnectionPool.TryTake(out SftpClient? connection))
            {
                lock (_syncLock)
                {
                    if (_currentConnections < _maxConnections)
                    {
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了SFtpClient");
                        connection = new SftpClient(Host, Port, UserName, _privateKeyFiles);
                        connection.Connect();
                        // 创建时不需要添加到池中, 用完返回进池就可以了
                        //_sftpConnectionPool.Add(connection);
                        _currentConnections++;
                    }
                    else
                    {
                        throw new InvalidOperationException("Reached maximum number of connections.");
                    }
                }
            }
            if (connection is null)
            {
                throw new Exception($"从连接池获取的SftpClient对象为空, Host: {Host}");
            }

            if (!connection.IsConnected)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 检测到连接池中的SFTP连接异常断开, 重新连接xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                connection.Connect();
            }

            return connection ?? throw new Exception("Sftp connection is null");
        }
        /// <summary>
        /// 连接返回连接池
        /// </summary>
        /// <param name="connection"></param>
        private void ReturnConnection(SshClient connection)
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
        private void ReturnConnection(SftpClient connection)
        {
            if (connection != null)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 执行完毕SFTP返回连接池");
                _sftpConnectionPool.Add(connection);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            foreach (var connection in _sshConnectionPool)
            {
                connection?.Disconnect();
                connection?.Dispose();
                _currentConnections--;
            }

            foreach (var connection in _sftpConnectionPool)
            {
                connection?.Disconnect();
                connection?.Dispose();
                _currentConnections--;
            }
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// 构造函数, 提供远程服务器信息
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="username"></param>
        /// <param name="privateKey"></param>
        public SshHelper(string host, int port, string username, string privateKey)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 创建了一个SshHelper对象");
            Host = host;
            Port = port == 0 ? 22 : port;
            UserName = username;
            _privateKeyFiles = privateKey.Split([';', ','], StringSplitOptions.RemoveEmptyEntries).Select(x => new PrivateKeyFile(x)).ToArray(); //new PrivateKeyFile(privateKey);
            if (_privateKeyFiles.Length == 0)
            {
                string defaultPrivateKey = Environment.OSVersion.Platform == PlatformID.Win32NT ? "C:/Users/Wu Qianlin/.ssh/id_ed25519" : "/root/.ssh/id_ed25519";
                _privateKeyFiles = [new PrivateKeyFile(defaultPrivateKey)];
            }
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



#pragma warning disable CS1570 // XML 注释出现 XML 格式错误
        /// <summary>
        /// 运行命令
        /// shell命令
        /// 上传 upload   {LOCAL} {REMOTE 1.不存在则认为是目录;2存在情况2.1文件覆盖;2.2目录则上传该位置} -include={要求只上传文件名包含该子字符串的文件,多个用','隔开} -exclude={文件名包含指定字符串则不上传,多个用','隔开} 
        /// 下载 download {LOCAL} {REMOTE} -include={要求只上传文件名包含该子字符串的文件,多个用','隔开} -exclude={文件名包含指定字符串则不上传,多个用','隔开} 
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<SshCommand?> RunCommandAsync(string command)
#pragma warning restore CS1570 // XML 注释出现 XML 格式错误
        {
            var cmdMatch = RegexConst.CommandRegex.Match(command);
            var action = cmdMatch.Groups["action"].Value.Replace('\\', '/');
            var local = cmdMatch.Groups["local"].Value.Replace('\\', '/');
            var remote = cmdMatch.Groups["remote"].Value.Replace('\\', '/');
            // ["form.api.dll", ".pfx", "Dockfile"]
            var includes = cmdMatch.Groups["include"].Value.Replace('\\', '/')?.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var excludes = cmdMatch.Groups["exclude"].Value.Replace('\\', '/')?.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (action.ToLower() == "upload")
            {
                if (string.IsNullOrWhiteSpace(local) && string.IsNullOrWhiteSpace(remote))
                {
                    throw new Exception("上传文件格式错误: " + command);
                }
                await UploadAsync(local, remote, includes, excludes);
                return null;
            }
            else if (action.ToLower() == "download")
            {
                if (string.IsNullOrWhiteSpace(local) && string.IsNullOrWhiteSpace(remote))
                {
                    throw new Exception("上传文件格式错误: " + command);
                }
                await DownloadAsync(local, remote, includes, excludes);
                return null;
            }
            else
            {
                var conn = await GetConnectionAsync();
                var sshCommand = conn.RunCommand(command);
                ReturnConnection(conn);
                return sshCommand;
            }
        }
        private async Task UploadAsync(string local, string remotePath, string[]? includes, string[]? excludes)
        {
            var conn = GetSftpConnection();

            var localFiles = new List<string>();
            bool uploadDirectory = Directory.Exists(local);
            foreach (var item in remotePath.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }
                string remote = item.Trim();
                // 上传目录
                if (uploadDirectory)
                {
                    #region 上传目录
                    // 统一目录格式
                    local = local.TrimEnd('.');
                    remote = remote.TrimEnd('.');
                    local = local.EndsWith('/') ? local : $"{local}/";
                    remote = remote.EndsWith('/') ? remote : $"{remote}/";

                    await EnsureDirectoryExistAsync(remote, conn);
                    localFiles = FileHelper.FindFilesRecursive(local, f => includes == null || !includes.Any() || includes.Any(part => Path.GetFileName(f).Contains(part)) && (excludes == null || excludes.All(part => f.IndexOf(part) == -1)), null, null);
                    foreach (var localFile in localFiles)
                    {
                        var localFileRelativePath = localFile.Replace(local, "");
                        var targetFilePath = Path.Combine(remote, localFileRelativePath);
                        using var fs = File.OpenRead(localFile);

                        var targetDirectory = Path.GetDirectoryName(targetFilePath)?.Replace('\\', '/');
                        if (string.IsNullOrWhiteSpace(targetDirectory))
                        {
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 上传文件, 获取服务器文件的目录失败[{targetFilePath}]");
                            continue;
                        }
                        await EnsureDirectoryExistAsync(targetDirectory, conn);

                        try
                        {
                            conn.UploadFile(fs, targetFilePath);
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 文件已上传: {targetFilePath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 上传文件[{localFile}] -> [{targetFilePath}]异常: {ex.Message}");
                            throw;
                        }
                    }
                    #endregion
                }
                else if (File.Exists(local))
                {
                    #region 上传文件
                    string localFileName = local.Split('/').Last();
                    using var fs = File.OpenRead(local);
                    if (conn.Exists(remote))
                    {
                        var remoteAttributes = conn.GetAttributes(remote);
                        if (remoteAttributes.IsDirectory)
                        {
                            remote = Path.Combine(remote, localFileName).Replace('\\', '/');
                        }
                    }
                    else
                    {
                        var ssh = await GetConnectionAsync();
                        ssh.RunCommand($"mkdir -p {remote}");
                        ReturnConnection(ssh);
                        remote = Path.Combine(remote, localFileName).Replace('\\', '/');
                    }
                    conn.UploadFile(fs, remote);
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 上传文件成功: {localFileName} -> {remote}");
                    localFiles.Add(local);
                    #endregion
                }
                else
                {
                    throw new Exception($"本地路径:{local}不存在, 无法上传");
                }
            }
            
            ReturnConnection(conn);
        }

        private async Task DownloadAsync(string local, string remote, string[]? includes, string[]? excludes)
        {
            var conn = GetSftpConnection();
            if (!conn.Exists(remote))
            {
                throw new Exception($"远程下载: [{remote}]不存在");
            }
            var remoteAttributes = conn.GetAttributes(remote);
            if (remoteAttributes.IsDirectory)
            {
                #region 下载目录
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 下载目录: {remote}");
                // 下载目录
                // 统一目录格式
                local = local.TrimEnd('.');
                remote = remote.TrimEnd('.');
                local = local.EndsWith('/') ? local : $"{local}/";
                remote = remote.EndsWith('/') ? remote : $"{remote}/";

                // 获取远程目录下所有文件
                var remoteFiles = await GetRemoteFiles(remote, includes, excludes);

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
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 文件已下载: {relativePath} -> {localfile}");
                }
                #endregion
            }
            else
            {
                // 下载文件
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 下载文件: {local}");
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
        }
        private async Task EnsureDirectoryExistAsync(string remoteDirectory, SftpClient? conn)
        {
            bool localConn = false;
            if (conn is null)
            {
                localConn = true;
                conn = GetSftpConnection();
            }
            if (!conn.Exists(remoteDirectory))
            {
                var ssh = await GetConnectionAsync();
                ssh.RunCommand($"mkdir -p {remoteDirectory}");
                ReturnConnection(ssh);
            }
            if (localConn)
            {
                ReturnConnection(conn);
            }
        }
        /// <summary>
        /// 读取远程文件
        /// </summary>
        /// <param name="remotePath"></param>
        /// <param name="includes"></param>
        /// <param name="excludes"></param>
        /// <returns></returns>
        public async Task<string[]> GetRemoteFiles(string remotePath, string[]? includes, string[]? excludes)
        {
            #region 生成正则
            string includePattern = string.Empty;
            if (includes is not null && includes.Any())
            {
                foreach (var include in includes)
                {
                    includePattern += $"{include}\\|";
                }
                includePattern = includePattern.TrimEnd('|').TrimEnd('\\');
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

            var ssh = await GetConnectionAsync();
            var filesResult = ssh.RunCommand($"find {remotePath} -type f {includePattern} {excludePattern} -maxdepth 4 | head -n 100000").Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ReturnConnection(ssh);
            return filesResult;
        }
        /// <summary>
        /// 使用ssh连接主机执行命令
        /// </summary>
        /// <param name="command"></param>
        public async Task<CommandResult> ExecuteAsync(string command)
        {
            var cmd = await RunCommandAsync(command);
            var output = cmd?.Result ?? "";
            var error = cmd?.Error ?? "";

            if (string.IsNullOrWhiteSpace(error))
            {
                return new(true, output);
            }
            return new(false, error);
        }

        /// <summary>
        /// 使用SSH连接主机处理文件, 服务器上没有该文件则上传; 如果文件上传了, 那么也需要将处理后的文件下载下来
        /// </summary>
        /// <param name="localPathSourceFile">文件在form环境中的路径, 如: C:/temp/temp.doc</param>
        /// <param name="remotePathSourceFile">文件上传到服务器上的路径, 如: /home/administrator/web/attachments/Convert/temp.doc</param>
        /// <param name="localPathResultFile">处理后的文件在form环境中的路径, 如: C:/temp/temp.pdf</param>
        /// <param name="remotePathResultFile">处理后的文件在服务器上的路径, 如: /home/administrator/web/attachments/Convert/temp.pdf</param>
        /// <param name="command">处理文件的命令</param>
        public async Task<Tuple<bool, string>> HandleFile(string localPathSourceFile, string remotePathSourceFile, string localPathResultFile, string remotePathResultFile, string command)
        {
            remotePathSourceFile = remotePathSourceFile.Replace("\\", "/");

            remotePathResultFile = remotePathResultFile.Replace("\\", "/");

            command = command.Replace("\\", "/");

            // 如果是容器环境, 远程主机是宿主机, 文件可能已经已经挂载到宿主机, 不需要上传
            var sftp = GetSftpConnection();
            var client = await GetConnectionAsync();
            var fileRemoteExist = sftp.Exists(remotePathSourceFile);

            bool fileUploaded = false;
            if (!fileRemoteExist)
            {
                var remoteDir = Path.GetDirectoryName(remotePathSourceFile)?.Replace('\\', '/') ?? throw new Exception($"获取目录名称失败: {remotePathSourceFile}");
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
            await cmd.ExecuteAsync();
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

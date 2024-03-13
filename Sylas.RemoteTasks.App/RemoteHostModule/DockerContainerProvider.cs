namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// docker容器信息提供者
    /// </summary>
    public class DockerContainerProvider : RemoteHostInfoProvider
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="remoteHost"></param>
        /// <exception cref="Exception"></exception>
        public DockerContainerProvider(RemoteHost remoteHost)
            : base(remoteHost ?? throw new Exception("DockerContainerManger注入RemoteHost为空"))
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} DI容器注入DockerContainerManger单例对象(注入依赖RemoteHost单例, RemoteHostInfoFactory单例) {remoteHost.Ip}");
        }

        /// <summary>
        /// 重写获取服务器信息的获取: 获取docker容器信息
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<RemoteHostInfo> BuildRemoteHostInfos()
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Invoke the RemoteHostInfos() method, query remote host's INFOS in real-time.");
            var ssh = RemoteHost.SshConnection;
            var dockerPs = ssh.RunCommandAsync("docker ps --format \"{{.ID}} && {{.Image}} && {{.Command}} && {{.CreatedAt}} && {{.Status}} && {{.Ports}} && {{.Names}}\"").GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(dockerPs?.Error))
            {
                yield break;
            }

            var lines = dockerPs?.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines is not null)
            {
                foreach (var line in lines)
                {
                    var parts = line.Split("&&", StringSplitOptions.TrimEntries);
                    #region 创建DockerContainerInfo
                    var dockerContainerInfo = new DockerContainer
                    {
                        ContainerId = parts[0],
                        Image = parts[1],
                        Command = parts[2],
                        Created = parts[3],
                        Status = parts[4],
                        Ports = parts[5],
                        Names = parts[6]
                    };
                    #endregion
                    yield return dockerContainerInfo;
                }
            }
            yield break;
        }
    }
}

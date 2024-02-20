namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class RemoteHostInfoProviderDockerContainer : RemoteHostInfoProvider
    {
        public RemoteHostInfoProviderDockerContainer(RemoteHost remoteHost, ContainerFactory remoteHostInfoFactory)
            : base(remoteHost ?? throw new Exception("DockerContainerManger注入RemoteHost为空"),
                  remoteHostInfoFactory ?? throw new Exception("DockerContainerManger注入RemoteHostInfoFactory为空"))
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} DI容器注入DockerContainerManger单例对象(注入依赖RemoteHost单例, RemoteHostInfoFactory单例) {remoteHost.Ip}");
        }

        /// <summary>
        /// 多次调用会多次查询
        /// </summary>
        public override List<RemoteHostInfo> RemoteHostInfos()
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Invoke the RemoteHostInfos() method, query remote host's INFOS in real-time.");
            if (RemoteHost is null)
            {
                throw new Exception($"DockerContainerManger.RemoteHost没有初始化");
            }
            var result = new List<RemoteHostInfo>();
            var ssh = RemoteHost.SshConnection;
            var dockerPs = ssh.RunCommand("docker ps --format \"{{.ID}} && {{.Image}} && {{.Command}} && {{.CreatedAt}} && {{.Status}} && {{.Ports}} && {{.Names}}\"");
            if (!string.IsNullOrWhiteSpace(dockerPs?.Error))
            {
                return [];
            }

            var lines = dockerPs?.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines is not null)
            {
                foreach (var line in lines)
                {
                    var parts = line.Split("&&", StringSplitOptions.TrimEntries);
                    var hostInfo = RemoteHostInfoFactory.GetHostDockerContainer(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], RemoteHost.Ip);
                    result.Add(hostInfo);
                }
            }
            return result;
        }
    }
}

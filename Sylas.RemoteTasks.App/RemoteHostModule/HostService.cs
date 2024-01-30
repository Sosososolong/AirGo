namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class HostService(IConfiguration configuration,
                       ILoggerFactory loggerFactory,
                       List<RemoteHost> remoteHosts,
                       List<RemoteHostInfoProvider> hostInfoPrividers)
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly List<RemoteHostInfoProvider> _hostInfoProviders = hostInfoPrividers;
        private readonly ILogger _logger = loggerFactory.CreateLogger<HostService>();
        private readonly List<RemoteHost>? _remoteHosts = remoteHosts ?? [];

        /// <summary>
        /// 获取所有主机的信息管理器
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns></returns>
        public List<RemoteHostInfoProvider> GetHostsManagers()
        {
            return _hostInfoProviders;
        }

        public object Execute(ExecuteDto executeDto)
        {
            var hostInfoManager = _hostInfoProviders.FirstOrDefault(x => x.RemoteHost.Ip == executeDto.HostIp);
            if (hostInfoManager is null)
            {
                return new { code = -1, msg = "未找到远程主机对应的信息管理器" };
            }
            var remoteHostInfo = hostInfoManager.RemoteHostInfos().FirstOrDefault(x => x.Name == executeDto.HostInfoName);
            if (remoteHostInfo is not null)
            {
                var infoCmd = remoteHostInfo.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName && x.CommandTxt == executeDto.Command);
                if (infoCmd is null)
                {
                    return new { code = -1, msg = "位置的命令" };
                }
                Console.WriteLine($"从主机信息(容器)执行命令: {infoCmd.CommandTxt}");
                var exeCmd = hostInfoManager.RemoteHost.SshConnection.RunCommand(infoCmd.CommandTxt);
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            var remoteHostCmd = hostInfoManager.RemoteHost.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName);
            if (remoteHostCmd is not null)
            {
                var exeCmd = hostInfoManager.RemoteHost.SshConnection.RunCommand(remoteHostCmd.CommandTxt);
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            return new { code = -1, msg = "未找到对应的远程主机信息" };
        }
    }
}

using Sylas.RemoteTasks.App.RemoteHostModule.Anything;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class HostService(IConfiguration configuration,
                       ILoggerFactory loggerFactory,
                       List<RemoteHost> remoteHosts,
                       IServiceProvider serviceProvider)
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger _logger = loggerFactory.CreateLogger<HostService>();
        private readonly List<RemoteHost>? _remoteHosts = remoteHosts ?? [];

        List<RemoteHostInfoProvider>? _hostInfoProviders = null;
        List<RemoteHostInfoProvider> HostInfoProviders
        {
            get
            {
                if (_hostInfoProviders is null)
                {
                    _hostInfoProviders = [];
                    foreach (var remoteHost in remoteHosts)
                    {
                        var dockerContainerProvider = new DockerContainerProvider(remoteHost);
                        _hostInfoProviders.Add(dockerContainerProvider);
                    }
                }
                
                return _hostInfoProviders;
            }
        }
        /// <summary>
        /// 获取所有主机的信息管理器
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns></returns>
        public List<RemoteHostInfoProvider> GetHostsManagers()
        {
            return HostInfoProviders;
        }

        public object Execute(ExecuteDto executeDto)
        {
            var hostInfoProvider = HostInfoProviders.FirstOrDefault(x => x.RemoteHost.Ip == executeDto.HostIp);
            if (hostInfoProvider is null)
            {
                return new { code = -1, msg = "未找到远程主机对应的信息管理器" };
            }
            var remoteHostInfo = hostInfoProvider.RemoteHostInfos().FirstOrDefault(x => x.Name == executeDto.HostInfoName);
            if (remoteHostInfo is not null)
            {
                var infoCmd = remoteHostInfo.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName && x.CommandTxt == executeDto.Command);
                if (infoCmd is null)
                {
                    return new { code = -1, msg = "位置的命令" };
                }
                Console.WriteLine($"从主机信息(容器)执行命令: {infoCmd.CommandTxt}");
                var exeCmd = hostInfoProvider.RemoteHost.SshConnection.RunCommand(infoCmd.CommandTxt);
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            var remoteHostCmd = hostInfoProvider.RemoteHost.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName);
            if (remoteHostCmd is not null)
            {
                var exeCmd = hostInfoProvider.RemoteHost.SshConnection.RunCommand(remoteHostCmd.CommandTxt);
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            return new { code = -1, msg = "未找到对应的远程主机信息" };
        }

        public List<AnythingInfoOutDto> GetAnythingInfos()
        {
            return AnythingInfo.AnythingInfos;
        }
    }
}

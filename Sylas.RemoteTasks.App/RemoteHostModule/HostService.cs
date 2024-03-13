using Sylas.RemoteTasks.App.RemoteHostModule.Anything;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class HostService(IConfiguration configuration,
                       ILoggerFactory loggerFactory,
                       List<RemoteHost> remoteHosts,
                       List<RemoteHostInfoProvider> remoteHostInfoProviders,
                       IServiceProvider serviceProvider)
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger _logger = loggerFactory.CreateLogger<HostService>();
        private readonly List<RemoteHost>? _remoteHosts = remoteHosts ?? [];
        private readonly List<RemoteHostInfoProvider> _hostInfoProviders = remoteHostInfoProviders;
        /// <summary>
        /// 获取所有主机的信息
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns></returns>
        public List<RemoteHostInfoProvider> GetRemoteHosts()
        {
            return _hostInfoProviders;
        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="executeDto"></param>
        /// <returns></returns>
        public object Execute(ExecuteDto executeDto)
        {
            var hostInfoProvider = _hostInfoProviders.FirstOrDefault(x => x.RemoteHost.Ip == executeDto.HostIp);
            if (hostInfoProvider is null)
            {
                return new { code = -1, msg = "未找到远程主机对应的信息提供者" };
            }
            var remoteHostInfo = hostInfoProvider.GetRemoteHostInfos().FirstOrDefault(x => x.Name == executeDto.HostInfoName);
            if (remoteHostInfo is not null)
            {
                var infoCmd = remoteHostInfo.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName && x.CommandTxt == executeDto.Command);
                if (infoCmd is null)
                {
                    return new { code = -1, msg = "位置的命令" };
                }
                Console.WriteLine($"从主机信息(容器)执行命令: {infoCmd.CommandTxt}");
                var exeCmd = hostInfoProvider.RemoteHost.SshConnection.RunCommandAsync(infoCmd.CommandTxt).GetAwaiter().GetResult();
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            var remoteHostCmd = hostInfoProvider.RemoteHost.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName);
            if (remoteHostCmd is not null)
            {
                var exeCmd = hostInfoProvider.RemoteHost.SshConnection.RunCommandAsync(remoteHostCmd.CommandTxt).GetAwaiter().GetResult();
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            return new { code = -1, msg = "未找到对应的远程主机信息" };
        }
    }
}

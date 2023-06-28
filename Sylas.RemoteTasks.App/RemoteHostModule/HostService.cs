using MySqlX.XDevAPI.Common;
using Renci.SshNet;
using Sylas.RemoteTasks.App.Utils;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class HostService
    {
        private readonly IConfiguration _configuration;
        private readonly List<RemoteHostInfoManager> _hostInfoManagers;
        //private readonly RemoteHostInfoFactory _infoFactory;
        //private readonly RemoteHostInfoManagerFactory _infoManangerFactory;
        private readonly ILogger _logger;
        private List<RemoteHost>? _remoteHosts;

        public HostService(IConfiguration configuration,
                           ILoggerFactory loggerFactory,
                           List<RemoteHost> remoteHosts,
                           //RemoteHostInfoFactory infoFactory,
                           //RemoteHostInfoManagerFactory infoManangerFactory
                           List<RemoteHostInfoManager> hostInfoManager
            )
        {
            _configuration = configuration;
            _hostInfoManagers = hostInfoManager;
            //_infoFactory = infoFactory;
            //_infoManangerFactory = infoManangerFactory;
            _logger = loggerFactory.CreateLogger<HostService>();

            _remoteHosts = remoteHosts ?? new List<RemoteHost>();
        }

        /// <summary>
        /// 获取所有主机的信息管理器
        /// </summary>
        /// <param name="hosts"></param>
        /// <returns></returns>
        public List<RemoteHostInfoManager> GetHostsManagers()
        {
            return _hostInfoManagers;
        }
        
        public object Execute(ExecuteDto executeDto)
        {
            var hostInfoManager = _hostInfoManagers.FirstOrDefault(x => x.RemoteHost.Ip == executeDto.HostIp);
            if (hostInfoManager is null)
            {
                return new { code = -1, msg = "未找到远程主机对应的信息管理器" };
            }
            var remoteHostInfo = hostInfoManager.RemoteHostInfos().FirstOrDefault(x => x.Name == executeDto.HostInfoName);
            if (remoteHostInfo is not null)
            {
                var infoCmd = remoteHostInfo.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName && x.Command == executeDto.Command);
                if (infoCmd is null)
                {
                    return new { code = -1, msg = "位置的命令" };
                }
                Console.WriteLine($"从主机信息(容器)执行命令: {infoCmd.Command}");
                var exeCmd = hostInfoManager.RemoteHost.SshConnection.RunCommand(infoCmd.Command);
                return new { code = 1, msg = exeCmd.Result, error = exeCmd.Error };
            }
            var remoteHostCmd = hostInfoManager.RemoteHost.Commands.FirstOrDefault(x => x.Name == executeDto.CommandName);
            if (remoteHostCmd is not null)
            {
                var exeCmd = hostInfoManager.RemoteHost.SshConnection.RunCommand(remoteHostCmd.Command);
                return new { code = 1, msg = exeCmd?.Result, error = exeCmd?.Error };
            }
            return new { code = -1, msg = "未找到对应的远程主机信息" };
        }
    }
}

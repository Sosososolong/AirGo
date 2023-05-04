using System.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public static class StartupHelper
    {
        public static void AddRemoteHostManager(this IServiceCollection services, IConfiguration configuration)
        {
            var remoteHosts = configuration.GetSection("Hosts").Get<List<RemoteHost>>() ?? new List<RemoteHost>();
            
            services.AddSingleton(remoteHosts);

            // TODO: 没有配置的给默认的值
            services.Configure<List<RemoteHostInfoCommandSettings>>(configuration.GetSection("RemoteHostInfoCommandSettings"));

            services.AddSingleton<RemoteHostInfoFactory>();

            services.AddSingleton(serviceProvider =>
            {
                var remoteHostInfoFactory = serviceProvider.GetService<RemoteHostInfoFactory>() ?? throw new Exception("DI容器中获取的RemoteHostInfoFactory为空");
                var result = new List<RemoteHostInfoManager>();
                foreach (var remoteHost in remoteHosts)
                {
                    var dockerContainerManager = new DockerContainerManger(remoteHost, remoteHostInfoFactory);
                    result.Add(dockerContainerManager);
                }
                return result;
            });
        }
    }
}

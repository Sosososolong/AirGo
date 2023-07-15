using System.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.RequestProcessor;

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
                    var dockerContainerManager = new RemoteHostInfoMangerDockerContainer(remoteHost, remoteHostInfoFactory);
                    result.Add(dockerContainerManager);
                }
                return result;
            });
        }

        public static void AddDatabaseUtils(this IServiceCollection services)
        {
            // DatabaseInfo每调用一次方法都会创建新的数据库连接, 所以单例也可以实现多线程操作数据库, 但是不同连接交给不同的对象, 有助于做状态存储管理
            services.AddTransient<DatabaseInfo>();

            services.AddTransient<IDatabaseProvider, DatabaseProvider>();
            services.AddTransient<IDatabaseProvider, DatabaseInfo>();

            services.AddTransient<DatabaseInfoFactory>();
        }

        public static class LiftTimeTestContainer
        {
            public static List<RequestProcessorDataTable> requestProcessorDataTableApis { get; set; } = new List<RequestProcessorDataTable>();
            public static List<ILogger> requestProcessorDataTableApisLog { get; set; } = new List<ILogger>();
            public static List<IConfiguration> configurations { get; set; } = new List<IConfiguration>();
            public static List<IServiceProvider> serviceProviders { get; set; } = new List<IServiceProvider>();
            public static List<DatabaseInfo> databaseInfos { get; set; } = new List<DatabaseInfo>();
        }
    }
}

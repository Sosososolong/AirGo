using System.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
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
                    var dockerContainerManager = new DockerContainerManger(remoteHost, remoteHostInfoFactory);
                    result.Add(dockerContainerManager);
                }
                return result;
            });
        }

        public static void AddDatabaseInfo(this IServiceCollection services)
        {
            //services.AddScoped((serviceProvider) =>
            //{
            //    IConfiguration configuration = serviceProvider.GetService<IConfiguration>() ?? throw new Exception("DI容器中获取IConfiguration实例失败");
            //    string connectionString = configuration.GetConnectionString("Default") ?? throw new Exception("数据库连接字符串为空");
            //    return new DatabaseInfo(serviceProvider, connectionString);
            //});
            services.AddScoped<DatabaseInfo>();
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

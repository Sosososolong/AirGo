using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.Test
{
    public class TestFixture
    {
        public ServiceProvider ServiceProvider { get; private set; }

        public TestFixture()
        {
            var serviceCollection = new ServiceCollection();
            // DI容器中注册所有的服务
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("parameters.log.json")
                .Build();

            // 添加日志
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(_configuration.GetSection("Logging"));
                // 这行代码将 "控制台日志提供程序" 添加到日志记录器构建器中。这意味着所有的日志消息将被发送到控制台
                loggingBuilder.AddConsole();
                // 这行代码将 "调试日志提供程序" 添加到日志记录器构建器中。这意味着所有的日志消息将被发送到调试输出窗口
                loggingBuilder.AddDebug();
            });

            services.AddSingleton<IConfiguration>(_configuration);

            #region Host
            var remoteHosts = _configuration.GetSection("Hosts").Get<List<RemoteHost>>() ?? new List<RemoteHost>();

            services.AddSingleton(remoteHosts);

            // TODO: 没有配置的给默认的值
            services.Configure<List<RemoteHostInfoCommandSettings>>(_configuration.GetSection("RemoteHostInfoCommandSettings"));
            //var commandTmplSettings = configuration.GetSection("RemoteHostInfoCommandSettings").Get<List<RemoteHostInfoCommandSettings>>() ?? new List<RemoteHostInfoCommandSettings>();
            //services.AddSingleton(commandTmplSettings);

            services.AddSingleton<ContainerFactory>();

            services.AddSingleton(serviceProvider =>
            {
                var remoteHostInfoFactory = serviceProvider.GetService<ContainerFactory>() ?? throw new Exception("DI容器中获取的RemoteHostInfoFactory为空");
                var result = new List<RemoteHostInfoProvider>();
                foreach (var remoteHost in remoteHosts)
                {
                    var dockerContainerManager = new RemoteHostInfoProviderDockerContainer(remoteHost, remoteHostInfoFactory);
                    result.Add(dockerContainerManager);
                }
                return result;
            });
            #endregion

            #region 仓储
            services.AddScoped(typeof(RepositoryBase<>), typeof(RepositoryBase<>));
            #endregion

            services.AddSingleton<HostService>();
            services.AddScoped<DatabaseInfo>();
            services.AddScoped<IDatabaseProvider, DatabaseInfo>();
        }
    }
}

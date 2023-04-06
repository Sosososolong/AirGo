using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sylas.RemoteTasks.App.RemoteHostModule;

namespace Sylas.RemoteTasks.Test
{
    public class TestFixture
    {
        public ServiceProvider ServiceProvider { get; private set; }

        public TestFixture()
        {
            var serviceCollection = new ServiceCollection();
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
            services.AddSingleton<HostService>();
        }
    }
}

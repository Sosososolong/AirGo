﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sylas.RemoteTasks.App.RemoteHostModule;
using System.Configuration;

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
            #endregion

            services.AddSingleton<HostService>();
        }
    }
}
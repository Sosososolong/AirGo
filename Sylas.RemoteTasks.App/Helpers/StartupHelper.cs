﻿using Microsoft.Extensions.Options;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;

namespace Sylas.RemoteTasks.App.Helpers
{
    public static class StartupHelper
    {
        public static void AddRemoteHostManager(this IServiceCollection services, IConfiguration configuration)
        {
            var remoteHosts = configuration.GetSection("Hosts").Get<List<RemoteHost>>() ?? [];

            services.AddSingleton(remoteHosts);

            services.AddSingleton(serviceProvider =>
            {
                var result = new List<RemoteHostInfoProvider>();
                foreach (var remoteHost in remoteHosts)
                {
                    var dockerContainerProvider = new DockerContainerProvider(remoteHost);
                    result.Add(dockerContainerProvider);
                }
                return result;
            });
        }

        public static void AddDatabaseUtils(this IServiceCollection services)
        {
            // DatabaseInfo每调用一次方法都会创建新的数据库连接, 所以单例也可以实现多线程操作数据库
            // 但是不同连接交给不同的对象, 有助于做状态存储管理, 所以可以考虑使用Transient;
            // Scoped有个好处是, 一次请求处理多个HttpReqeustProcessorSteps的时候会处理多个HttpHandler,
            //   只要第一个HttpHandler的参数设置了连接字符串就会被存入到DatabaseInfo实例中, 一次请求中其他的HttpHandler都会使用此DatabaseInfo, 都不用再重新设置连接字符串参数了
            //   如果一次请求中也需要使用多个DatabaseInfo, 那么可以使用DatabaseInfoFactory创建一个新的对象, 并且可以继承线程内的DatabaseInfo的配置
            // 从这里看, Scoped是比较灵活的方式, 特定情况下它也可以实现Transient的效果
            services.AddScoped<DatabaseInfo>();

            // 即时创建新的DatabaseInfo, 单例即可
            services.AddSingleton<DatabaseInfoFactory>();

            services.AddScoped<IDatabaseProvider, DatabaseProvider>();
            services.AddScoped<IDatabaseProvider, DatabaseInfo>();
        }

        public static void AddGlobalHotKeys(this IServiceCollection services, IConfiguration configuration)
        {
            if (Environment.OSVersion.VersionString.Contains("Windows"))
            {
                var globalHotKeys = configuration.GetChildren().Where(x => x.Key == "GlobalHotKeys").First().GetChildren();
                int hotKeyId = 1;
                List<GlobalHotKey> globalHotKeyList = [];
                foreach (var globalHotKey in globalHotKeys)
                {
                    if (globalHotKey.Value is null)
                    {
                        break;
                    }
                    var keys = globalHotKey.Value.Split('+').ToList();
                    var lastKey = keys.Last();
                    keys.Remove(lastKey);
                    globalHotKeyList.Add(new GlobalHotKey(hotKeyId++, keys, lastKey));
                }
                SystemHelper.RegisterGlobalHotKey(globalHotKeyList);
            }
        }
    }
}

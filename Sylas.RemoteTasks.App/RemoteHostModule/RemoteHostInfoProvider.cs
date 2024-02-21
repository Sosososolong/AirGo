using Sylas.RemoteTasks.Utils.Template;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// 远程服务器信息提供者抽象类, 子类根据需要去获取Docker容器等信息
    /// </summary>
    public abstract class RemoteHostInfoProvider
    {
        protected RemoteHostInfoProvider(RemoteHost remoteHost)
        {
            RemoteHost = remoteHost;
        }
        public RemoteHost RemoteHost { get; set; }
        public List<RemoteHostInfo> GetRemoteHostInfos()
        {
            var result = new List<RemoteHostInfo>();
            IEnumerable<RemoteHostInfo> remoteHostInfos = BuildRemoteHostInfos();
            foreach (var remoteHostInfo in remoteHostInfos)
            {
                var hostInfo = BuildHostInfoWithCommands(remoteHostInfo);
                result.Add(hostInfo);
            }
            return result;
        }

        /// <summary>
        /// 子类根据业务实现自己的RemoteHostInfo
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<RemoteHostInfo> BuildRemoteHostInfos();

        /// <summary>
        /// 根据配置的命令, 解析HostInfo对应的命令
        /// </summary>
        /// <param name="dockerContainerInfo"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public RemoteHostInfo BuildHostInfoWithCommands(RemoteHostInfo dockerContainerInfo)
        {
            // 根据模板配置解析DockerContainerInfo的命令集
            List<CommandInfo> resolvedCommands = [];
            foreach (var commandTmpl in RemoteHost.HostInfoCommands)
            {
                string resolvedCommandTxt = TmplHelper.ResolveExpressionValue(commandTmpl.CommandTxt, dockerContainerInfo)?.ToString() ?? throw new Exception($"失败");
                resolvedCommands.Add(new CommandInfo { Name = commandTmpl.Name, CommandTxt = resolvedCommandTxt });
            }

            dockerContainerInfo.Commands = resolvedCommands;
            return dockerContainerInfo;
        }
    }
}

using Microsoft.Extensions.Options;
using Sylas.RemoteTasks.App.RegexExp;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class ContainerFactory(IOptions<List<RemoteHostInfoCommandSettings>> tmplSettings)
    {
        private readonly List<RemoteHostInfoCommandSettings> _tmplSettings = tmplSettings.Value;

        public RemoteHostInfo GetHostDockerContainer(string containerId, string image, string command, string created, string status, string ports, string names, string host)
        {
            #region 创建DockerContainerInfo
            var dockerContainerInfo = new RemoteHostInfoDockerContainer
            {
                ContainerId = containerId,
                Image = image,
                Command = command,
                Created = created,
                Status = status,
                Ports = ports,
                Names = names
            };
            #endregion

            #region 根据模板配置计算出DockerContainerInfo的命令集
            var currentHostTmplSetting = _tmplSettings.FirstOrDefault(x => x.Host == host);
            var dockerContainerProperties = typeof(RemoteHostInfoDockerContainer).GetProperties();
            if (currentHostTmplSetting is not null)
            {
                foreach (var commandTmpl in currentHostTmplSetting.RemoteHostInfoCommands)
                {
                    commandTmpl.CommandTxt = RegexConst.CurrentObjPropTmpl.Replace(commandTmpl.CommandTxt, match =>
                    {
                        var propName = match.Groups["propName"].Value;
                        var propValue = dockerContainerProperties.FirstOrDefault(p => p.Name == propName)?.GetValue(dockerContainerInfo)?.ToString();
                        return propValue ?? match.Value;
                    });
                }
            }

            dockerContainerInfo.Commands = currentHostTmplSetting?.RemoteHostInfoCommands ?? [];

            return dockerContainerInfo;
            #endregion
        }
    }
}

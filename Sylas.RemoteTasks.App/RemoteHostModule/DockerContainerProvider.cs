using Microsoft.Extensions.Options;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.Utils.Template;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class DockerContainerProvider : RemoteHostInfoProvider
    {
        public DockerContainerProvider(RemoteHost remoteHost)
            : base(remoteHost ?? throw new Exception("DockerContainerManger注入RemoteHost为空"))
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} DI容器注入DockerContainerManger单例对象(注入依赖RemoteHost单例, RemoteHostInfoFactory单例) {remoteHost.Ip}");
        }

        /// <summary>
        /// 多次调用会多次查询
        /// </summary>
        public override List<RemoteHostInfo> RemoteHostInfos()
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Invoke the RemoteHostInfos() method, query remote host's INFOS in real-time.");
            if (RemoteHost is null)
            {
                throw new Exception($"DockerContainerManger.RemoteHost没有初始化");
            }
            var result = new List<RemoteHostInfo>();
            var ssh = RemoteHost.SshConnection;
            var dockerPs = ssh.RunCommand("docker ps --format \"{{.ID}} && {{.Image}} && {{.Command}} && {{.CreatedAt}} && {{.Status}} && {{.Ports}} && {{.Names}}\"");
            if (!string.IsNullOrWhiteSpace(dockerPs?.Error))
            {
                return [];
            }

            var lines = dockerPs?.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines is not null)
            {
                foreach (var line in lines)
                {
                    var parts = line.Split("&&", StringSplitOptions.TrimEntries);
                    var hostInfo = BuildHostInfoWithCommands(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], RemoteHost.Ip);
                    result.Add(hostInfo);
                }
            }
            return result;
        }

        public RemoteHostInfo BuildHostInfoWithCommands(params object[] args)
        {
            if (args.Length != 8)
            {
                throw new Exception("DockerContainerFactory获取容器信息提供的参数异常");
            }
            string containerId = args[0].ToString() ?? "";
            string image = args[1].ToString() ?? "";
            string command = args[2].ToString() ?? "";
            string created = args[3].ToString() ?? "";
            string status = args[4].ToString() ?? "";
            string ports = args[5].ToString() ?? "";
            string names = args[6].ToString() ?? "";
            string host = args[7].ToString() ?? "";

            #region 创建DockerContainerInfo
            var dockerContainerInfo = new DockerContainer
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

            #region 根据模板配置解析DockerContainerInfo的命令集
            var dataContext = new Dictionary<string, object>()
            {
                { "Name", dockerContainerInfo.Name },
                { "Description", dockerContainerInfo.Description },
                { "ContainerId", containerId },
                { "Image", image },
                { "Command", command },
                { "Created", created },
                { "Status", status },
                { "Ports", ports },
                { "Names", names },
            };
            foreach (var commandTmpl in RemoteHost.HostInfoCommands)
            {
                commandTmpl.CommandTxt = TmplHelper.ResolveExpressionValue(commandTmpl.CommandTxt, dataContext)?.ToString() ?? throw new Exception($"失败");
            }

            dockerContainerInfo.Commands = RemoteHost.HostInfoCommands;

            return dockerContainerInfo;
            #endregion
        }
    }
}

using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Template;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    /// <summary>
    /// 用来描述任何需要操作的对象
    /// </summary>
    public class AnythingInfo(ICommandExecutor executor)
    {
        /// <summary>
        /// 标识, 使用模板从属性中获取
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 用于显示, 使用模板从属性中获取
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 可执行命令
        /// </summary>
        public List<CommandInfo> Commands { get; set; } = [];

        /// <summary>
        /// 给当前对象自定义属性
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = [];

        public string CommandExecutorTemplate { get; set; } = "";
        private ICommandExecutor CommandExecutor { get; set; } = executor;

        static readonly List<AnythingInfo> _anythings = [];
        public static List<AnythingInfoOutDto> AnythingInfos { get { return [.. _anythings.Select(x => new AnythingInfoOutDto() { Name = x.Name, Title = x.Title, Commands = [.. x.Commands.Select(c => c.Name)]  })]; } }
        static readonly Dictionary<string, object> _environment1 = new()
        {
            { "Ip", "192.168.1.229" },
            { "Port", 22 },
            { "Title", "DEV Web服务器" },
        };
        static readonly Dictionary<string, object> _environment2 = new()
        {
            { "Ip", "192.168.1.230" },
            { "Port", 22 },
            { "Title", "DEV数据库服务器" },
        };
        static AnythingInfo()
        {
            List<AnythingSettingDto> anythingSettings = [
                new AnythingSettingDto()
                {
                    Properties = new() { { "Ip", "192.168.1.229" }, { "Port", 22 }, { "Title", "DEV Web服务器" } },
                    Name = "$Ip",
                    Title = "$Title",
                    Commands = [
                        new CommandInfo { Name = "开启TCP转发", CommandTxt = "ssh 192.168.1.2 dev_tcp_forward_off" },
                        new CommandInfo { Name = "查看网盘目录", CommandTxt = "ls /home/administrator/web/seafilelocal/平台发布包/" },
                        new CommandInfo { Name = "查看所有容器", CommandTxt = "docker ps" }
                    ],
                    CommandExecutorTemplate = "SshHelper(${Ip},${Port},root,C:/Users/Wu Qianlin/.ssh/id_ed25519)"
                },
                new AnythingSettingDto()
                {
                    Properties = new() { { "Ip", "192.168.1.229" }, { "Port", 22 }, { "Name", "my-centos7-code-server" }, { "Title", "centos7容器包含code-server环境" }, { "ImageName", "my-centos7:code-server" } },
                    Name = "$Name",
                    Title = "$Title",
                    Commands = [
                        new CommandInfo { Name = "停止", CommandTxt = "docker stop ${Name}" },
                        new CommandInfo { Name = "停止;删除", CommandTxt = "docker stop ${Name}; docker rm ${Name};" },
                        new CommandInfo { Name = "停止;删除;删除镜像", CommandTxt = "docker stop ${Name}; docker rm ${Name}; docker rmi ${ImageName}" },
                        new CommandInfo { Name = "重新构建镜像", CommandTxt = "docker stop ${Name}; docker rm ${Name}; docker rmi ${ImageName}; cd /usr/local/code-server/ && docker build -t ${ImageName} ." },
                        new CommandInfo { Name = "重新构建镜像并运行容器", CommandTxt = "docker stop ${Name}; docker rm ${Name}; docker rmi ${ImageName}; cd /usr/local/code-server/ && docker build -t ${ImageName} .; docker run -it -d --name ${Name} --privileged=true -p 50627:22 -p 50628:50819 -v \"$$HOME/.config/code-server:/root/.config/code-server\" -v \"/$$HOME/.nuget:/root/.nuget\" -v \"/$$HOME/.local/share/code-server/extensions:/root/.local/share/code-server/extensions\" -u \"$$(id -u):$$(id -g)\" -e \"DOCKER_USER=$$USER\" ${ImageName}" }
                    ],
                    CommandExecutorTemplate = "SshHelper(${Ip},${Port},root,C:/Users/Wu Qianlin/.ssh/id_ed25519)"
                },
                new AnythingSettingDto()
                {
                    Properties = new() { { "Ip", "192.168.1.229" }, { "Port", 22 }, { "Name", "my-centos7-base" }, { "Title", "centos7容器ssh服务环境" }, { "ImageName", "my-centos7:base" } },
                    Name = "$Name",
                    Title = "$Title",
                    Commands = [
                        new CommandInfo { Name = "停止", CommandTxt = "docker stop ${Name}" },
                        new CommandInfo { Name = "停止;删除", CommandTxt = "docker stop ${Name}; docker rm ${Name};" },
                        new CommandInfo { Name = "停止;删除;删除镜像", CommandTxt = "docker stop ${Name}; docker rm ${Name}; docker rmi ${ImageName}" },
                        new CommandInfo { Name = "重新构建镜像", CommandTxt = "docker stop ${Name}; docker rm ${Name}; docker rmi ${ImageName}; cd /usr/local/code-server/ && docker build -t ${ImageName} ." },
                        new CommandInfo { Name = "重新构建镜像并运行容器", CommandTxt = "docker stop ${Name}; docker rm ${Name}; docker rmi ${ImageName}; cd /usr/local/code-server/ && docker build -t ${ImageName} .; docker run -it -d --name ${Name} --privileged=true -p 50627:22 -p 50628:50819 -v \"$$HOME/.config/code-server:/root/.config/code-server\" -v \"/$$HOME/.nuget:/root/.nuget\" -v \"/$$HOME/.local/share/code-server/extensions:/root/.local/share/code-server/extensions\" -u \"$$(id -u):$$(id -g)\" -e \"DOCKER_USER=$$USER\" ${ImageName}" }
                    ],
                    CommandExecutorTemplate = "SshHelper(${Ip},${Port},root,C:/Users/Wu Qianlin/.ssh/id_ed25519)"
                }
            ];
            foreach (var anything in anythingSettings)
            {
                anything.Properties.ResolveSelfTmplValues();
                anything.Name = TmplHelper.ResolveExpressionValue(anything.Name, anything.Properties)?.ToString() ?? throw new Exception($"Name {anything.Name} 解析失败");
                anything.Title = TmplHelper.ResolveExpressionValue(anything.Title, anything.Properties)?.ToString() ?? throw new Exception($"Title {anything.Title} 解析失败");
                #region 解析Executor
                if (string.IsNullOrWhiteSpace(anything.CommandExecutorTemplate))
                {
                    anything.CommandExecutorTemplate = "SystemCmd()";
                }
                var match = Regex.Match(anything.CommandExecutorTemplate, @"(?<ExecutorName>\w+)\((?<Args>.*)\)");
                var executorName = match.Groups["ExecutorName"].Value;
                var argsTmpl = match.Groups["Args"].Value;
                object[] args = [];
                if (!string.IsNullOrWhiteSpace(argsTmpl))
                {
                    var argTmpls = argsTmpl.Split(',');
                    args = new object[argTmpls.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        args[i] = TmplHelper.ResolveExpressionValue(argTmpls[i], anything.Properties);
                    }
                }
                var t = ReflectionHelper.GetTypeByClassName(executorName);
                var instance = ReflectionHelper.CreateInstance(t, args);
                var executeCommandMethod = t.GetMethod("ExecuteCommand");
                var anythingCommandExecutor = instance as ICommandExecutor ?? throw new Exception("从模板生成ICommandExecutor失败");
                #endregion
                #region 解析CommandTxt
                foreach (var anythingCommand in anything.Commands)
                {
                    anythingCommand.CommandTxt = TmplHelper.ResolveExpressionValue(anythingCommand.CommandTxt, anything.Properties)?.ToString() ?? throw new Exception($"解析命令\"{anythingCommand.CommandTxt}\"异常");
                }
                #endregion
                _anythings.Add(new AnythingInfo(anythingCommandExecutor) { Name = anything.Name, Title = anything.Title, Properties = anything.Properties, Commands = anything.Commands, CommandExecutorTemplate = anything.CommandExecutorTemplate });
            }
        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<CommandResult> ExecuteAsync(CommandInfoInDto dto)
        {
            var anythingInfo =_anythings.FirstOrDefault(x => x.Name == dto.Anything) ?? throw new Exception($"未知的操作对象{dto.Anything}");
            var commandInfo = anythingInfo.Commands.FirstOrDefault(x => x.Name == dto.Command) ?? throw new Exception($"未知的命令{dto.Command}");
            CommandResult cr = await anythingInfo.CommandExecutor.ExecuteAsync(commandInfo.CommandTxt);
            return cr;
        }
    }

    /// <summary>
    /// Anything的配置模型
    /// </summary>
    public class AnythingSettingDto()
    {
        /// <summary>
        /// 标识, 使用模板从属性中获取
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 用于显示, 使用模板从属性中获取
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 可执行命令
        /// </summary>
        public List<CommandInfo> Commands { get; set; } = [];

        /// <summary>
        /// 给当前对象自定义属性
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = [];

        public string CommandExecutorTemplate { get; set; } = "";
    }
}

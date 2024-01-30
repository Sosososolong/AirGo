using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Template;
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
        static AnythingInfo()
        {
            List<AnythingSettingDto> anythingSettings = [
                new AnythingSettingDto()
                {
                    Properties = new Dictionary<string, object>
                    {
                        { "Name", "192.168.1.229" },
                        { "Port", 22 },
                        { "Title", "DEV环境({{Name}})" },
                    },
                    Name = "$Name",
                    Title = "$Title",
                    Commands = [
                        new CommandInfo { Name = "开启TCP转发", CommandTxt = "ssh 192.168.1.2 dev_tcp_forward_off" },
                        new CommandInfo { Name = "查看网盘目录", CommandTxt = "ls /home/administrator/web/seafilelocal/平台发布包/" },
                        new CommandInfo { Name = "查看所有容器", CommandTxt = "docker ps" }
                    ],
                    CommandExecutorTemplate = "SshHelper(${Name},${Port},root,C:/Users/Wu Qianlin/.ssh/id_ed25519)"
                },
                //new AnythingSettingDto()
                //{
                //    Properties = new Dictionary<string, object>
                //    {
                //        { "Name", "iduo.form.api" },
                //        { "Title", "操作5004" }
                //    },
                //    Name = "$Name",
                //    Title = "$Title",
                //    Commands = [
                //        new CommandInfo { Name = "重启", CommandTxt = "docker logs {{Name}}" },
                //        new CommandInfo { Name = "重新发布", CommandTxt = "docker stop {{Name}};docker rm {{Name}};docker rmi {{Name}};cd /www/wwwroot/iduo.server/;docker-compose up -d" },
                //    ],
                //    CommandExecutorTemplate = "SshHelper(${Name},${Port},root,C:/Users/Wu Qianlin/.ssh/id_ed25519)"
                //}
            ];
            foreach (var anything in anythingSettings)
            {
                anything.Properties.ResolveSelfTmplValues();
                anything.Name = TmplHelper.ResolveExpressionValue(anything.Name, anything.Properties)?.ToString() ?? throw new Exception($"Name {anything.Name} 解析失败");
                anything.Title = TmplHelper.ResolveExpressionValue(anything.Title, anything.Properties)?.ToString() ?? throw new Exception($"Title {anything.Title} 解析失败");
                #region 解析Executor
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
                _anythings.Add(new AnythingInfo(anythingCommandExecutor) { Name = anything.Name, Title = anything.Title, Properties = anything.Properties, Commands = anything.Commands, CommandExecutorTemplate = anything.CommandExecutorTemplate });
            }
        }
        public static object Test()
        {
            
            StringBuilder executeCommandBuilder = new();

            foreach (var anything in _anythings)
            {
                foreach (var command in anything.Commands)
                {
                    executeCommandBuilder.AppendLine(command.Name);
                    CommandResult cr = anything.CommandExecutor.ExecuteCommand(command.CommandTxt);
                    executeCommandBuilder.AppendLine($"{(cr.Succeed ? "执行成功" : "执行失败:")}{Environment.NewLine}{cr.Message}");
                    executeCommandBuilder.AppendLine();
                }
            }
            return executeCommandBuilder.ToString();
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

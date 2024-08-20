using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Template;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Remote
{
    public class SshHelperTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper = outputHelper;
        private readonly DatabaseInfo _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();

        [Fact]
        public async Task RunCommand_UploadFileAsync()
        {
            string host = _configuration["SshHelperTest:Host"] ?? throw new Exception("测试上传文件失败: 远程服务器未配置");
            int port = _configuration.GetSection("SshHelperTest").GetValue<int>("Port");
            string username = _configuration["SshHelperTest:User"] ?? string.Empty;
            string privateKeys = _configuration["SshHelperTest:PrivateFiles"] ?? string.Empty;
            string localPath = _configuration["SshHelperTest:LocalPath"] ?? throw new Exception("测试上传文件失败: 未配置需要上传的文件或目录");
            string remotePaths = _configuration["SshHelperTest:RemotePaths"] ?? throw new Exception("测试上传文件失败: 未配置上传到服务器保存的位置");
            using SshHelper sshHelper = new(host, port, username, privateKeys);

            //await sshHelper.RunCommandAsync($"upload {localPath} {remotePaths}");

            string[]? commands = _configuration.GetSection("SshHelperTest:Commands").Get<string[]>() ?? throw new Exception("没有站点重载SSL证书命令");
            Dictionary<string, object> env = new()
            {
                { "KeystorePass", "123456" }
            };
            foreach (var command in commands)
            {
                var cmd = TmplHelper.ResolveExpressionValue(command, env).ToString();
                _outputHelper.WriteLine(cmd);
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    _outputHelper.WriteLine($"\"{cmd}\"经过模板转换后为空");
                    return;
                }
                var commandResult = await sshHelper.RunCommandAsync(cmd);
                if (!string.IsNullOrWhiteSpace(commandResult?.Error))
                {
                    _outputHelper.WriteLine(commandResult?.Error);
                }

                if (!string.IsNullOrWhiteSpace(commandResult?.Result))
                {
                    _outputHelper.WriteLine(commandResult?.Result);
                }
            }
        }
    }
}

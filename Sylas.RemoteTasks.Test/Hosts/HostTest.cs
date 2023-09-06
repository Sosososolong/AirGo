using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Hosts
{

    public class HostTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly HostService _hostService;

        public HostTest(TestFixture fixture, ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _hostService = fixture.ServiceProvider.GetRequiredService<HostService>();
        }

        [Fact]
        public void GetHostInfoListTest()
        {
            var remoteHostManagers = _hostService.GetHostsManagers();
            for (int i = 0; i < remoteHostManagers.Count; i++)
            {
                var infos = remoteHostManagers[i].RemoteHostInfos();
                foreach (var info in infos)
                {
                    _outputHelper.WriteLine(info.Description);
                }
            }
        }

        [Fact]
        public void GetRemoteFilesTest()
        {
            var sshhelper = new SshHelper("192.168.1.229", 22, "root", "C:/Users/Wu Qianlin/.ssh/id_ed25519");
            var result = sshhelper.GetRemoteFiles("/home/administrator/web/Id.Form.Api", null, null);
        }
    }
}

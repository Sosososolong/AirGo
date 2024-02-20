using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Hosts
{

    public class HostTest(TestFixture fixture, ITestOutputHelper outputHelper) : IClassFixture<TestFixture>
    {
        private readonly HostService _hostService = fixture.ServiceProvider.GetRequiredService<HostService>();

        [Fact]
        public void GetHostInfoListTest()
        {
            var remoteHostManagers = _hostService.GetHostsManagers();
            for (int i = 0; i < remoteHostManagers.Count; i++)
            {
                var infos = remoteHostManagers[i].RemoteHostInfos();
                foreach (var info in infos)
                {
                    outputHelper.WriteLine(info.Description);
                }
            }
        }
    }
}

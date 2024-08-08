using Newtonsoft.Json;
using Sylas.RemoteTasks.Utils;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.SystemHelperTest
{
    public class ShellTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        //private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        [Fact]
        public async Task ExecuteAsync_FindMySqlDNginx()
        {
            var startTime = DateTime.Now;
            string[] commands = ["tasklist | findstr \"mysql\"", "tasklist | findstr \"nginx\"", "netstat -ano | findstr \"3306\""];
            var res = await SystemCmd.ExecuteAsync(commands);
            for (int i = 0; i < res.Count; i++)
            {
                outputHelper.WriteLine($"{commands[i]}: {res[i]}");
            }
            var t1 = DateTime.Now;
            outputHelper.WriteLine($"-------------------------{(t1 - startTime).TotalMilliseconds}/ms------------------------------");
            res = await SystemCmd.ExecuteParallellyAsync(commands);
            for (int i = 0; i < res.Count; i++)
            {
                outputHelper.WriteLine($"{commands[i]}: {res[i]}");
            }
            var t2 = DateTime.Now;
            outputHelper.WriteLine($"-------------------------{(t2 - t1).TotalMilliseconds}/ms------------------------------");
        }
        [Fact]
        public async Task GetServerAndAppInfo()
        {
            var res = await SystemCmd.GetServerAndAppInfoAsync();
            outputHelper.WriteLine(JsonConvert.SerializeObject(res));
        }
    }
}

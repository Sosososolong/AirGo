using Newtonsoft.Json;
using Sylas.RemoteTasks.Utils;
using System.Collections.Concurrent;
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

        [Fact]
        public async Task GetProcessCpuAndRam()
        {
            var t1 = DateTime.Now;
            var res = await SystemCmd.GetProcessCpuAndRamAsync("nginx");
            var t2 = DateTime.Now;
            outputHelper.WriteLine($"-------------------------{(t2 - t1).TotalMilliseconds}/ms------------------------------");
            outputHelper.WriteLine(JsonConvert.SerializeObject(res));

            res = await SystemCmd.GetProcessCpuAndRamAsync("devenv");
            var t3 = DateTime.Now;
            outputHelper.WriteLine($"{Environment.NewLine}-------------------------{(t3 - t2).TotalMilliseconds}/ms------------------------------");
            outputHelper.WriteLine(JsonConvert.SerializeObject(res));

            string[] pns = ["nginx", "devenv"];
            ConcurrentBag<string> result = [];
            Task[] tasks = pns.Select(async pn =>
            {
                var res = await SystemCmd.GetProcessCpuAndRamAsync(pn);
                result.Add(JsonConvert.SerializeObject(res));
            }).ToArray();
            await Task.WhenAll(tasks);
            var t4 = DateTime.Now;
            outputHelper.WriteLine($"{Environment.NewLine}-------------------------{(t4 - t3).TotalMilliseconds}/ms------------------------------");
            outputHelper.WriteLine(JsonConvert.SerializeObject(result.ToList()));
        }
    }
}

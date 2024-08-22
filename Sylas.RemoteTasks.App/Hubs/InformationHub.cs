using Microsoft.AspNetCore.SignalR;
using Sylas.RemoteTasks.Utils;
using System.Collections.Concurrent;

namespace Sylas.RemoteTasks.App.Hubs
{
    /// <summary>
    /// 服务端推送一些信息
    /// </summary>
    /// <param name="configuration"></param>
    public class InformationHub(IConfiguration configuration) : Hub
    {
        static bool _stopGetProcessesStatus = true;
        public async Task StartGetProcessesStatus()
        {
            _stopGetProcessesStatus = false;
            List<string>? processNames = configuration.GetSection("ProcessMonitor:Names").Get<List<string>>();
            if (processNames is null || processNames.Count == 0)
            {
                await Clients.Caller.SendAsync("ClientShowProcessesStatus", new List<string>());
                return;
            }
            while (true)
            {
                ConcurrentBag<string> statusList = [];
                List<Task> tasks = [];
                foreach (string pn in processNames)
                {
                    tasks.Add(GetProcessStatusAsync(pn, statusList));
                }
                await Task.WhenAll(tasks);
                await Clients.Caller.SendAsync("ClientShowProcessesStatus", statusList.OrderBy(x => x));
                if (_stopGetProcessesStatus)
                {
                    break;
                }
            }

            static async Task GetProcessStatusAsync(string pn, ConcurrentBag<string> statusList)
            {
                List<string> pnStatus = await SystemCmd.GetProcessCpuAndRamAsync(pn);
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 获取Cpu和内存占用:{pn}");
                foreach (var item in pnStatus)
                {
                    statusList.Add(item);
                }
            }
            //await Clients.All.SendAsync("ReceiveInfo", info);
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} OnDisconnectedAsync");
            _stopGetProcessesStatus = true;
            return base.OnDisconnectedAsync(exception);
        }
    }
}

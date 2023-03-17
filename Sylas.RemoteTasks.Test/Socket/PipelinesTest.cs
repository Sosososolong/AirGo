using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Socket
{
    public class PipelinesTest
    {
        private readonly ITestOutputHelper _outputHelper;

        public PipelinesTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }
        [Fact]
        public async Task LinesServerWithSocketTest()
        {
            await App.Utils.SocketHelper.MyProcessLinesAsync();
        }
    }
}

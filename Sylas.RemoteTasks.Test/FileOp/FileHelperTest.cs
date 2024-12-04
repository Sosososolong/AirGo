using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.FileOp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.FileOp
{
    public class FileHelperTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DatabaseInfo _databaseInfo;
        private readonly IConfiguration _configuration;
        public FileHelperTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        }
    }
}

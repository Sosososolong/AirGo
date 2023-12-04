using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Test
{
    public class TestBase(IConfiguration configuration) : IClassFixture<TestFixture>
    {
        protected readonly IConfiguration _configuration = configuration;
    }
}

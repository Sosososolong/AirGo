using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Test
{
    public class TestBase
    {
        protected readonly IConfiguration _configuration;
        public TestBase()
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("parameters.log.json")
                .Build();
        }
    }
}

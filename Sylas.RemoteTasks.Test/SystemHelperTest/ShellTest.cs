using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.SystemHelperTest
{
    public class ShellTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly IConfiguration _configuration;

        public ShellTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        }
    }
}

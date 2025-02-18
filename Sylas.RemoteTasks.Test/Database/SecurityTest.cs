using Sylas.RemoteTasks.Common;
using System.Text;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Database
{
    public class SecurityTest
    {
        private readonly ITestOutputHelper _outputHelper;

        public SecurityTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }
        /// <summary>
        /// AES加密解密字节数组测试
        /// </summary>
        [Fact]
        public async Task AesBytesTestAsync()
        {
            string originText = "你";
            byte[] bytes = Encoding.UTF8.GetBytes(originText);
            var encrypted = await SecurityHelper.AesEncryptAsync(bytes);
            var decrypted = await SecurityHelper.AesDecryptAsync(encrypted);
            string original = Encoding.UTF8.GetString(decrypted);
            _outputHelper.WriteLine(original);
        }
    }
}

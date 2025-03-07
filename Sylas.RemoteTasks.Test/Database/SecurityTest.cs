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
        /// 解密加密的数据库连接字符串
        /// </summary>
        [Fact]
        public async Task ShowEncryptedConnectionString()
        {
            _outputHelper.WriteLine(await SecurityHelper.AesDecryptAsync("ViRWPtMT4OTtqMuUnabhKhd/FL2nsar9p31b87fEnOLQGaHB5G6eMHWwLEOd9GqVbazdAC7pi7KPbVoyO8J1ia+R6skjDYAq1uOYXgGeIjRSEb2c5Wzsn7HXjzjBvhQTc2AonuhEnHekECdzW0uNcBfyd0tfSKWmX0I2pjk0pumSLTy5StBjw1GX4XRTRGdyFR4yt8RdVnk9ASlXA2DmwD84MgyNvGZtd1zlDH5/ctLm0qoLFq6v5n9m1qwAeQM/R2b6uViT3h63KWLn9Kt9iLOUzszrkN511YEynosUuX0="));
            _outputHelper.WriteLine(await SecurityHelper.AesDecryptAsync("ViRWPtMT4OTtqMuUnabhKhd/FL2nsar9p31b87fEnOLQGaHB5G6eMHWwLEOd9GqVaHN9tvbv3goQ3v4VpOaOhn6AQzSQfRRJy2xnkXi6kdUte2ebCMhlCcy1ugNZmxgU8GJPQEl3xRFy3IlJU9KzmD9jrE6hty1Fqv6LNRO9IkKvb8gxJPksChMH4sPFA5QaDJCaff7IH/uHyFRHq706hAKuY1AE0RIBUAleIeaGvglXpwecMtY4IVs58gsuLLtrop2rq1S6+W+fo7UqhDHXYd+R8VhaAFn6BdD1LGOeFCo="));
            _outputHelper.WriteLine(await SecurityHelper.AesDecryptAsync("ViRWPtMT4OTtqMuUnabhKhd/FL2nsar9p31b87fEnOLQGaHB5G6eMHWwLEOd9GqVZJ80Hp9cfhW0YWp80MUhDG8EwqgodFx9iUUCOnOYDA2mc9DzShwr9qRWJjnSms6IhgF/5kvH+k8LtVlJGnvjPQ4aKdUoy5T9mfz+wtko6QTFfip1Zp1VwDl4vj/kv/0eDvO0pDPPhLmoxRCp085Kv3g21TQGhTbYEgayWxM8vgURrzHiFLZykDaOzi+VlaPoy6jExf767NrYWMEtc5qJnrZsF8oMDsx0nnw6huCJX9Q="));
            _outputHelper.WriteLine(await SecurityHelper.AesDecryptAsync("iL2dnOfV40AsdfFtHMLkhypbia2FU16+n7Tl9+pTuvyorigLzqcYkIJ/q7KlOwrLLovGGrcolJ6qOmvsg24qGfvTUg7c7C6ERNhpdHMHY18DyT4DYZEMfTEc9tTFnSihaf2MZXshrU1mJ2kR/6NybeulLBIjiDvpBjz7h+L3BKABubK5ZgCjF/8cOAYcGJNM/H1JYJ3IkOAItiBvOUXdLw=="));
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

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 数据安全,加密解密帮助类
    /// </summary>
    public static class SecurityHelper
    {
        static byte[] GetBytesFromString(string key, int length = 32)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length < length)
            {
                var newKeyBytes = new byte[length];
                keyBytes.CopyTo(newKeyBytes, 0);
                keyBytes = newKeyBytes;
            }
            else if (keyBytes.Length > length)
            {
                keyBytes = keyBytes.Take(length).ToArray();
            }
            return keyBytes;
        }
        /// <summary>
        ///  AES加密
        /// </summary>
        /// <param name="original">明文</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static async Task<string> AesEncryptAsync(string original, string key = "")
        {
            // Create a new instance of the Aes class.
            using Aes myAes = CreateAes(key);

            // Encrypt the string to an array of bytes.
            byte[] encrypted = await EncryptStringToBytesByAesAsync(original, myAes);

            string encryptedString = Convert.ToBase64String(encrypted);
            return encryptedString;
        }
        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encrypedStr"></param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static async Task<string> AesDecryptAsync(string encrypedStr, string key = "")
        {
            var encrypedBytes = Convert.FromBase64String(encrypedStr);
            using Aes myAes = CreateAes(key);
            // Decrypt the bytes to a string.
            string roundtrip = await DecryptBytesToStringByAes(encrypedBytes, myAes);
            return roundtrip;
        }

        /// <summary>
        ///  AES加密
        /// </summary>
        /// <param name="original">明文</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static async Task<byte[]> AesEncryptAsync(byte[] original, string key = "")
        {
            // Create a new instance of the Aes class.
            using Aes myAes = CreateAes(key);

            byte[] encrypted = await EncryptBytesByAesAsync(original, myAes);
            return encrypted;
        }
        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encrypedBytes"></param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static async Task<byte[]> AesDecryptAsync(byte[] encrypedBytes, string key = "")
        {
            using Aes myAes = CreateAes(key);
            // Decrypt the bytes to a string.
            byte[] original = await DecryptBytesByAesAsync(encrypedBytes, myAes);
            return original;
        }

        /// <summary>
        /// 移除无效的混淆字符
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string RemoveConfusedChars(this string source)
        {
            return source.Replace("^.s-", "s").Replace("^.-", "");
        }

        /// <summary>
        /// 创建一个Aes对象用于加密或者解密
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        static Aes CreateAes(string key = "")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "Aes@2024*_S(j.Fl*j张三@E$dF";
            }
            Aes myAes = Aes.Create();
            myAes.Key = GetBytesFromString(key, 32);
            myAes.IV = Encoding.UTF8.GetBytes("xxxxxxxxxxxxx001");
            return myAes;
        }
        static async Task<byte[]> EncryptStringToBytesByAesAsync(string plainText, Aes aes)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            byte[] encrypted;

            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            // Create the streams used for encryption.
            using MemoryStream msEncrypt = new();
            using CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new(csEncrypt))
            {
                //Write all data to the stream.
                await swEncrypt.WriteAsync(plainText);
            }
            encrypted = msEncrypt.ToArray();

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        static async Task<string> DecryptBytesToStringByAes(byte[] cipherText, Aes aes)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = "";

            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            // Create the streams used for decryption.
            using (MemoryStream msDecrypt = new(cipherText))
            {
                using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
                using StreamReader srDecrypt = new(csDecrypt);

                // Read the decrypted bytes from the decrypting stream
                // and place them in a string.
                plaintext = await srDecrypt.ReadToEndAsync();
            }

            return plaintext;
        }

        static async Task<byte[]> EncryptBytesByAesAsync(byte[] original, Aes aes)
        {
            // Check arguments.
            if (original == null || original.Length <= 0)
                throw new ArgumentNullException("plainText");

            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using MemoryStream ms = new();
            using (CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write))
            {
                await cs.WriteAsync(original, 0, original.Length);
            }

            // Return the encrypted bytes from the memory stream.
            return ms.ToArray();
        }
        /// <summary>
        /// AES解密字节数组
        /// </summary>
        /// <param name="encryptedBytes"></param>
        /// <param name="aes"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        static async Task<byte[]> DecryptBytesByAesAsync(byte[] encryptedBytes, Aes aes)
        {
            // Check arguments.
            if (encryptedBytes == null || encryptedBytes.Length <= 0)
                throw new ArgumentNullException("cipherText");

            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using MemoryStream ms = new();
            using (CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Write))
            {
                await cs.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// 使用HmacSha256加密方式对数据进行签名
        /// </summary>
        /// <param name="secret">加密密钥</param>
        /// <param name="message">需要签名的数据</param>
        /// <returns></returns>
        public static string HmacSha256Signature(this string message, string secret)
        {
            var encoding = new UTF8Encoding();
            byte[] keyByte = encoding.GetBytes(secret);
            using var hmacsha256 = new HMACSHA256(keyByte);
            byte[] messageBytes = encoding.GetBytes(message);
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            var signature = Convert.ToBase64String(hashmessage);
            return signature;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
        public static string AesEncrypt(string original, string key = "")
        {
            // Create a new instance of the Aes class.
            using Aes myAes = CreateAes(key);

            // Encrypt the string to an array of bytes.
            byte[] encrypted = EncryptStringToBytes_Aes(original, myAes);

            string encryptedString = Convert.ToBase64String(encrypted);
            return encryptedString;
        }
        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encrypedStr"></param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string AesDecrypt(string encrypedStr, string key = "")
        {
            var encrypedBytes = Convert.FromBase64String(encrypedStr);
            using Aes myAes = CreateAes(key);
            // Decrypt the bytes to a string.
            string roundtrip = DecryptStringFromBytes_Aes(encrypedBytes, myAes);
            return roundtrip;

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
        static byte[] EncryptStringToBytes_Aes(string plainText, Aes aes)
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
                swEncrypt.Write(plainText);
            }
            encrypted = msEncrypt.ToArray();

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        static string DecryptStringFromBytes_Aes(byte[] cipherText, Aes aes)
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
                plaintext = srDecrypt.ReadToEnd();
            }

            return plaintext;
        }

    }
}

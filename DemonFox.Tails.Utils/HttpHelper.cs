using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DemonFox.Tails.Utils
{
    public class HttpHelper
    {
        /// <summary>
        /// 发送一个Http Get请求，返回字符串结果
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<string> HttpGetAsync(string url)
        {
            using (var http = new HttpClient())
            {
                //await异步等待回应
                var response = await http.GetAsync(url);
                //确保HTTP成功状态值
                response.EnsureSuccessStatusCode();

                //await异步读取最后的JSON（注意此时gzip已经被自动解压缩了，因为上面的AutomaticDecompression = DecompressionMethods.GZip）
                return await response.Content.ReadAsStringAsync();
            }
        }   
        
        /// <summary>
        /// 发送一个Http Post请求，返回字符串结果
        /// </summary>
        /// <param name="url"></param>
        /// <param name="dic"></param>
        /// <returns></returns>
        public static async Task<string> HttpPostAsync(string url, IDictionary<string, string> dic)
        {
            using (HttpClient client = new HttpClient())
            {
                FormUrlEncodedContent content = null;
                if (dic != null)
                {
                    content = new FormUrlEncodedContent(dic);
                }
                var response = await client.PostAsync(url, content);
                return await response.Content.ReadAsStringAsync();
            }
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="toEncrypt">加密串</param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string AESEncrypt(string toEncrypt, string key)
        {
            byte[] resultArray = Encrypt_byte(toEncrypt, key);
            return Convert.ToBase64String(resultArray, 0, resultArray.Length);
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="toEncrypt">加密串</param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static byte[] Encrypt_byte(string toEncrypt, string key)
        {
            byte[] keyArray = Encoding.UTF8.GetBytes(key);
            byte[] toEncryptArray = Encoding.UTF8.GetBytes(toEncrypt);
            RijndaelManaged rDel = new RijndaelManaged
            {
                Key = keyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            ICryptoTransform cTransform = rDel.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            //return Convert.ToBase64String(resultArray, 0, resultArray.Length);
            rDel.Dispose();            
            return resultArray;
        }


        /// <summary>
        ///  AES解密
        /// </summary>
        /// <param name="toDecrypt"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string AESDecrypt(string toDecrypt, string key)
        {
            byte[] keyArray = Encoding.UTF8.GetBytes(key);
            byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);
            RijndaelManaged rDel = new RijndaelManaged
            {
                Key = keyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            ICryptoTransform cTransform = rDel.CreateDecryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            rDel.Dispose();
            return Encoding.UTF8.GetString(resultArray);
        }

        /// <summary>
        /// 获取客户端IP 适用于.net mvc
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        //public static string GetClientIP(HttpContext context)
        //{

        //    string text = "0.0.0.0";
        //    string text2 = text;
        //    if (context == null)
        //    {
        //        return text2;
        //    }
        //    text2 = context.Request.ServerVariables["X_Forwarded_For"];
        //    if (string.IsNullOrEmpty(text2) || string.Compare("unknown", text2, true) == 0)
        //    {
        //        text2 = context.Request.ServerVariables["Proxy-Client-IP"];
        //    }
        //    if (string.IsNullOrEmpty(text2) || string.Compare("unknown", text2, true) == 0)
        //    {
        //        text2 = context.Request.ServerVariables["WL-Proxy-Client-IP"];
        //    }
        //    if (string.IsNullOrEmpty(text2) || string.Compare("unknown", text2, true) == 0)
        //    {
        //        text2 = context.Request.ServerVariables["HTTP_CLIENT_IP"];
        //    }
        //    if (string.IsNullOrEmpty(text2) || string.Compare("unknown", text2, true) == 0)
        //    {
        //        text2 = context.Request.ServerVariables["HTTP_VIA"];
        //    }
        //    if (string.IsNullOrEmpty(text2) || string.Compare("unknown", text2, true) == 0)
        //    {
        //        text2 = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
        //    }
        //    if (string.IsNullOrEmpty(text2) || string.Compare("unknown", text2, true) == 0)
        //    {
        //        text2 = context.Request.ServerVariables["Remote_Addr"];
        //    }
        //    if (text2 == "::1")
        //    {
        //        text2 = "127.0.0.1";
        //    }            
        //    if (text2 != text)
        //    {
        //        text2 = text2.Replace(" ", "");
        //        if (text2.IndexOf(",") > 0)
        //        {
        //            text2 = text2.Split(new char[]
        //            {
        //                ','
        //            })[0];
        //        }
        //    }
        //    return text2;

        //}
    }
}

using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// 统一 Auth 描述, 5 种类型
    /// </summary>
    public class AuthSpec
    {
        /// <summary>
        /// none, bearer, basic, apikey, custom
        /// </summary>
        public string Type { get; set; } = "none";
        
        /// <summary>
        /// bearer
        /// </summary>
        public string Token { get; set; } = string.Empty;
        
        /// <summary>
        /// basic 用户名
        /// </summary>
        public string Username { get; set; } = string.Empty;
        /// <summary>
        /// basic 密码
        /// </summary>
        public string Password { get; set; } = string.Empty;
        
        /// <summary>
        /// apiKey 键名
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        /// <summary>
        /// apiKey 值
        /// </summary>
        public string KeyValue { get; set; } = string.Empty;
        /// <summary>
        /// header or query
        /// </summary>
        public string KeyIn { get; set; } = "header";

        /// <summary>
        /// 自定义请求头
        /// </summary>
        public List<KvPair> CustomHeaders { get; set; } = [];
    }
}

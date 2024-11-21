using System;
using System.IO;

namespace Sylas.RemoteTasks.Utils.Constants
{
    /// <summary>
    /// 一些常量路径
    /// </summary>
    public class PathConstants
    {
        /// <summary>
        /// 用户家目录
        /// </summary>
        public static readonly string UserHomeDir = Environment.OSVersion.Platform == PlatformID.Win32NT ? Environment.GetEnvironmentVariable("USERPROFILE") : "~";
        /// <summary>
        /// 默认的SSH私钥文件路径(Ed25519算法)
        /// </summary>
        public static readonly string DefaultSshPrivateKeyFileEd25519 = Environment.OSVersion.Platform == PlatformID.Win32NT ? Path.Combine(UserHomeDir, ".ssh/id_ed25519") : "~/.ssh/id_ed25519";
        /// <summary>
        /// 默认的SSH私钥文件路径(Rsa算法)
        /// </summary>
        public static readonly string DefaultSshPrivateKeyFileRsa = Environment.OSVersion.Platform == PlatformID.Win32NT ? Path.Combine(UserHomeDir, ".ssh/id_rsa") : "~/.ssh/id_rsa";
    }
}

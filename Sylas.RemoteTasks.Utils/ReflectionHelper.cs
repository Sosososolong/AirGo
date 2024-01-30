using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 程序集类型
    /// </summary>
    public enum ProjectAssembly
    {
        /// <summary>
        /// 主程序所在程序集
        /// </summary>
        Main,
        /// <summary>
        /// 当前代码执行所在程序集
        /// </summary>
        Currrent
    }
    /// <summary>
    /// 反射帮助类
    /// </summary>
    public class ReflectionHelper
    {
        /// <summary>
        /// 获取项目启动程序中的所有类型
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IEnumerable<Type> GetCustomAssemblyTypes(ProjectAssembly assemblyType = ProjectAssembly.Main)
        {
            // xxx/Sylas.RemoteTasks/Sylas.RemoteTasks.App/bin/Debug/net7.0/Sylas.RemoteTasks.App.exe
            //string file = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? throw new Exception("获取当前进程的文件路径失败");
            // Sylas.RemoteTasks.App, Sylas.RemoteTasks.Utils
            
            string currentProcessName = Assembly.GetExecutingAssembly().FullName;
            // 获取主程序项目的程序集
            var customAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName) && x.FullName.StartsWith($"{currentProcessName.Split('.')[0]}."));
            return customAssemblies.SelectMany(s => s.GetTypes());
        }
        /// <summary>
        /// 根据类名获取类对应的Type
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Type GetTypeByClassName(string className)
        {
            var customAssemblyTypes = GetCustomAssemblyTypes();

            return customAssemblyTypes.FirstOrDefault(x => x.Name == className) ?? throw new Exception($"未找到类型: {className}");
        }
        /// <summary>
        /// 获取多个类型
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static Type[] GetTypes(Type baseType)
        {
            var customAssemblyTypes = GetCustomAssemblyTypes();
            return customAssemblyTypes.Where(x => baseType.IsAssignableFrom(x) && !x.IsInterface).ToArray();
        }

        /// <summary>
        /// 创建实例对象
        /// </summary>
        /// <param name="type"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static object? CreateInstance(Type type, params object[] args)
        {
            return Activator.CreateInstance(type, args);
        }
    }
}

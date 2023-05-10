using System.Reflection;

namespace Sylas.RemoteTasks.App.Utils
{
    public class ReflectionHelper
    {
        public static Type GetTypeByClassName(string className)
        {
            // 获取当前程序集
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            return currentAssembly.GetTypes().FirstOrDefault(x => x.Name == className) ?? throw new Exception($"未找到类型: {className}");
        }
    }
}

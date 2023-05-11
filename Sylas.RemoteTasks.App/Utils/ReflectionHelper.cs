using System.Reflection;

namespace Sylas.RemoteTasks.App.Utils
{
    public class ReflectionHelper
    {
        public static IEnumerable<Type> GetCustomAssemblyTypes()
        {
            string file = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? throw new Exception("获取当前进程的文件路径失败");
            string currentProcessName = Path.GetFileNameWithoutExtension(file);
            // 获取项目序集
            var customAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName) && x.FullName.StartsWith($"{currentProcessName.Split('.')[0]}."));
            return customAssemblies.SelectMany(s => s.GetTypes());
        }
        public static Type GetTypeByClassName(string className)
        {
            var customAssemblyTypes = GetCustomAssemblyTypes();

            return customAssemblyTypes.FirstOrDefault(x => x.Name == className) ?? throw new Exception($"未找到类型: {className}");
        }
        public static Type[] GetTypes(Type baseType)
        {
            var customAssemblyTypes = GetCustomAssemblyTypes();
            return customAssemblyTypes.Where(x => baseType.IsAssignableFrom(x) && !x.IsInterface).ToArray();
        }


        public static object? CreateInstance(Type type)
        {
            return Activator.CreateInstance(type);
        }
    }
}

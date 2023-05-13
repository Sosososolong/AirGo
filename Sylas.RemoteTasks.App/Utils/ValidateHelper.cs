namespace Sylas.RemoteTasks.App.Utils
{
    public class ValidateHelper
    {
        public static void ValidateArgumentIsNull<T>(T? instance, List<string> excludeProps)
        {
            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            var props = instance.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (excludeProps.Contains(prop.Name))
                {
                    continue;
                }
                var val = prop.GetValue(instance, null) ?? throw new ArgumentNullException(prop.Name);
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Controllers;

namespace Sylas.RemoteTasks.Test.Auth
{
    public class AuthorizeAttributeTest
    {
        [Fact]
        public void ApiAndMVCControllersShouldHaveAuthorizeAttribute()
        {
            var controllers = GetChildTypes<ControllerBase>();
            foreach (var controller in controllers)
            {
                var attribute = Attribute.GetCustomAttribute(controller, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true) as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
                Assert.NotNull(attribute);
            }
        }

        private static IEnumerable<Type> GetChildTypes<T>()
        {
            var types = typeof(HomeController).Assembly.GetTypes();
            return types.Where(t => t.IsSubclassOf(typeof(T)) && !t.IsAbstract);
        }
    }
}

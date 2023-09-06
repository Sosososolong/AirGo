using Microsoft.AspNetCore.Mvc;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class OAuthController : Controller
    {
        /// <summary>
        /// 测试微信钉钉第三方登录
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 密码模式
        /// </summary>
        /// <returns></returns>
        public IActionResult Password()
        {
            return View();
        }
    }
}

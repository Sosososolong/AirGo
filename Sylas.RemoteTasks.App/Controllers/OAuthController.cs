using Microsoft.AspNetCore.Mvc;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class OAuthController : Controller
    {
        /// <summary>
        /// 测试OAuth服务器的授权码授权
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            return View();
        }
    }
}

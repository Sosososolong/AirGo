using Microsoft.AspNetCore.Mvc;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class StudyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

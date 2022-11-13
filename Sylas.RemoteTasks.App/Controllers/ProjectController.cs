using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class ProjectController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GeneratProjectAsync()
        {
            string BaseDir = @"F:\工作\T11\BlogDemo.Auto";
            string CompanyName = "";
            string ProjectName = "BlogDemo";
            GeneratorContext generatorContext = new(BaseDir, CompanyName, ProjectName);
            string generatingInfo = await generatorContext.InitialProjectAsync();
            return Content(generatingInfo);
            //return Json(new JsonResultModel { Code = 1, Data = null, Message = generatingInfo });
        }
    }
}

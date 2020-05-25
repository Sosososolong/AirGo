using DemonFox.Tails.AirGo.Models;
using DemonFox.Tails.Infrastructure;
using DemonFox.Tails.Utils;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DemonFox.Tails.AirGo.Controllers
{
    [Route("CoreGenerator")]
    public class CoreGenerationController : Controller
    {
        [Route("Index")]
        public IActionResult Index()
        {            
            return View();
        }
        
        [Route("GenerateProject")]
        public async Task<IActionResult> GeneratProjectAsync()
        {
            string BaseDir = @"F:\工作\T11\BlogDemo.Auto";
            string CompanyName = "";
            string ProjectName = "BlogDemo";
            GeneratorContext generatorContext = new GeneratorContext(BaseDir, CompanyName, ProjectName);
            string generatingInfo = await generatorContext.InitialProjectAsync();
            return Content(generatingInfo);
            //return Json(new JsonResultModel { Code = 1, Data = null, Message = generatingInfo });
        }        
    }    
}

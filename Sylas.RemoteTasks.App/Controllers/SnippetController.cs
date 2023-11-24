using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.Snippets;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class SnippetController : Controller
    {
        private readonly ILogger<SnippetController> _logger;
        private readonly RepositoryBase<Snippet> _repository;

        public SnippetController(ILogger<SnippetController> logger, RepositoryBase<Snippet> repository)
        {
            _logger = logger;
            _repository = repository;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> List(int pageIndex, int pageSize)
        {
            var snippetPage = await _repository.GetPageAsync(pageIndex, pageSize, nameof(Snippet.Description), true, new DataFilter());
            return Json(snippetPage);
        }
    }
}

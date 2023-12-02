using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.Snippets;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class SnippetController : Controller
    {
        private readonly ILogger<SnippetController> _logger;
        private readonly RepositoryBase<Snippet> _snippetRepository;
        private readonly RepositoryBase<SnippetType> _snippetTypeRepository;

        public SnippetController(ILogger<SnippetController> logger, RepositoryBase<Snippet> repository, RepositoryBase<SnippetType> snippetTypeRepository)
        {
            _logger = logger;
            _snippetRepository = repository;
            _snippetTypeRepository = snippetTypeRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GetSnippets(int pageIndex, int pageSize, string orderField, bool isAsc = true)
        {
            if (string.IsNullOrWhiteSpace(orderField))
            {
                orderField = nameof(Snippet.Description);
            }
            var snippetPage = await _snippetRepository.GetPageAsync(pageIndex, pageSize, orderField, isAsc);
            return Json(snippetPage);
        }

        public async Task<IActionResult> GetSnippetTypes(string keyword)
        {
            Keywords keywords = string.IsNullOrWhiteSpace(keyword)
                ? new Keywords()
                : new Keywords { Fields = new[] { nameof(SnippetType.Name) }, Value = keyword };
            var typesPage = await _snippetTypeRepository.GetPageAsync(1, 100, filter: new DataFilter() { Keywords = keywords });
            return Json(typesPage);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Snippets;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Dto;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class SnippetController(RepositoryBase<Snippet> repository, RepositoryBase<SnippetType> snippetTypeRepository) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GetSnippets(int pageIndex, int pageSize, string orderField, bool isAsc = true, [FromBody] DataFilter? dataFilter = null)
        {
            if (string.IsNullOrWhiteSpace(orderField))
            {
                orderField = nameof(Snippet.Description);
            }
            var snippetPage = await repository.GetPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            return Json(snippetPage);
        }
        public async Task<IActionResult> AddSnippetAsync([FromBody] Snippet snippet)
        {
            var added = await repository.AddAsync(snippet);
            return added > 0 ? Ok(new OperationResult(true)) : BadRequest();
        }

        public async Task<IActionResult> UpdateSnippetAsync([FromBody] Snippet snippet)
        {
            var added = await repository.UpdateAsync(snippet);
            return added > 0 ? Ok(new OperationResult(true)) : BadRequest();
        }

        public async Task<IActionResult> GetSnippetTypesAsync(string keyword)
        {
            Keywords keywords = string.IsNullOrWhiteSpace(keyword)
                ? new Keywords()
                : new Keywords { Fields = [nameof(SnippetType.Name)], Value = keyword };
            var typesPage = await snippetTypeRepository.GetPageAsync(1, 100, filter: new DataFilter() { Keywords = keywords });
            return Json(typesPage);
        }

        public async Task<IActionResult> DeleteSnippetAsync([FromBody] int id)
        {
            var snippet = await repository.GetByIdAsync(id);
            if (snippet is null)
            {
                return Ok(new OperationResult(false, "内容不存在"));
            }
            var deleted = await repository.DeleteAsync(id);
            return deleted > 0 ? Ok(new OperationResult(true)) : Ok(new OperationResult(false, "删除失败"));
        }
    }
}

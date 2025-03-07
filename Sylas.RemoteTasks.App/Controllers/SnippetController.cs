using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Snippets;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class SnippetController(RepositoryBase<Snippet> repository, RepositoryBase<SnippetType> snippetTypeRepository) : CustomBaseController
    {
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GetSnippets([FromBody] DataSearch? search = null)
        {
            search ??= new();
            var snippetPage = await repository.GetPageAsync(search);
            return Json(new RequestResult<PagedData<Snippet>>(snippetPage));
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

        public async Task<IActionResult> GetSnippetTypesAsync(string keyword = "")
        {
            Keywords keywords = string.IsNullOrWhiteSpace(keyword)
                ? new Keywords()
                : new Keywords { Fields = [nameof(SnippetType.Name)], Value = keyword };
            var typesPage = await snippetTypeRepository.GetPageAsync(new(1, 100, filter: new DataFilter() { Keywords = keywords }));
            return Json(new RequestResult<PagedData<SnippetType>>(typesPage));
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

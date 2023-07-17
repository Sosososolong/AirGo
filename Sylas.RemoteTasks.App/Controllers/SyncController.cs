using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.RequestProcessor;
using Sylas.RemoteTasks.App.Utils;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class SyncController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SyncController> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpRequestProcessorRepository _repository;

        public SyncController(IHttpClientFactory httpClientFactory, ILogger<SyncController> logger, IConfiguration configuration, HttpRequestProcessorRepository repository)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _configuration = configuration;
            _repository = repository;
        }

        public async Task<IActionResult> Index([FromServices] RequestProcessorService service)
        {
            //await service.ExecuteFromAppsettingsAsync();

            return View();
        }

        public async Task<IActionResult> GetHttpRequestProcessorsAsync(int pageIndex, int pageSize, string orderField, bool isAsc, [FromBody]DataFilter dataFilter)
        {
            var processors = await _repository.GetPageAsync(pageIndex, pageSize, orderField, true, dataFilter);
            return Json(processors);
        }
    }
}

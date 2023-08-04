using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos;
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
            if (pageIndex == 0)
            {
                pageIndex = 1;
            }
            if (pageSize == 0)
            {
                pageSize = 10;
            }
            var processors = await _repository.GetPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            return Json(processors);
        }

        public async Task<IActionResult> GetHttpRequestProcessorStepsAsync(int pageIndex, int pageSize, string orderField, bool isAsc, [FromBody] DataFilter dataFilter)
        {
            var steps = await _repository.GetStepsPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            return Json(steps);
        }
        public async Task<IActionResult> GetHttpRequestProcessorStepDataHandlersAsync(int pageIndex, int pageSize, string orderField, bool isAsc, [FromBody] DataFilter dataFilter)
        {
            var steps = await _repository.GetDataHandlersPageAsync(pageIndex, pageSize, orderField, isAsc, dataFilter);
            return Json(steps);
        }

        #region HttpRequestProcessor
        public async Task<IActionResult> AddHttpRequestProcessorAsync([FromBody] HttpRequestProcessorCreateDto processor)
        {
            if (processor == null)
            {
                return Json(new OperationResult(false, "处理器不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Title))
            {
                return Json(new OperationResult(false, "处理器标题不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Name))
            {
                return Json(new OperationResult(false, "处理器名称/编码不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Url))
            {
                return Json(new OperationResult(false, "处理器Url不能为空"));
            }
            var result = await _repository.AddAsync(processor);
            if (result > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return BadRequest(result);
        }
        public async Task<IActionResult> UpdateHttpRequestProcessorAsync([FromBody] HttpRequestProcessor processor)
        {
            if (processor == null)
            {
                return Json(new OperationResult(false, "处理器不能为空"));
            }
            if (processor.Id == 0)
            {
                return Json(new OperationResult(false, "处理器标题不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Title))
            {
                return Json(new OperationResult(false, "处理器标题不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Name))
            {
                return Json(new OperationResult(false, "处理器名称/编码不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Url))
            {
                return Json(new OperationResult(false, "处理器Url不能为空"));
            }
            var dbProcessor = await _repository.GetByIdAsync(processor.Id);
            if (dbProcessor is null)
            {
                return NotFound("Http处理器不存在");
            }

            int affectedRows = await _repository.UpdateAsync(processor);
            if (affectedRows > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }
        public async Task<IActionResult> DeleteHttpRequestProcessorAsync([FromBody] string ids)
        {
            var idlist = ids.Split(',');
            int affectedRows = 0;
            foreach (var id in idlist)
            {
                affectedRows += await _repository.DeleteAsync(Convert.ToInt32(id));
            }
            
            if (affectedRows > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }
        #endregion



        
        #region HttpRequestProcessorStep
        /// <summary>
        /// 添加步骤Step
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddHttpRequestProcessorStepAsync([FromBody] HttpRequestProcessorStepCreateDto step)
        {
            if (step == null)
            {
                return Json(new OperationResult(false, "创建步骤为空"));
            }
            if (string.IsNullOrWhiteSpace(step.Parameters))
            {
                return Json(new OperationResult(false, "当前步骤执行参数不能为空"));
            }
            if (step.HttpRequestProcessorId == 0)
            {
                return Json(new OperationResult(false, "所属HTTP处理器不能为空"));
            }
            var result = await _repository.AddStepAsync(step);
            if (result > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return BadRequest(result);
        }
        /// <summary>
        /// 更新步骤Step
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateHttpRequestProcessorStepAsync([FromBody] HttpRequestProcessorStep step)
        {
            if (step == null)
            {
                return Json(new OperationResult(false, "创建步骤为空"));
            }
            if (string.IsNullOrWhiteSpace(step.Parameters))
            {
                return Json(new OperationResult(false, "当前步骤执行参数不能为空"));
            }
            if (step.HttpRequestProcessorId == 0)
            {
                return Json(new OperationResult(false, "所属HTTP处理器不能为空"));
            }
            var dbStep = await _repository.GetStepByIdAsync(step.Id);
            if (dbStep is null)
            {
                return NotFound("Http处理器步骤不存在");
            }

            int affectedRows = await _repository.UpdateStepAsync(step);
            if (affectedRows > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }
        /// <summary>
        /// 删除步骤Step
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteHttpRequestProcessorStepAsync([FromBody] string ids)
        {
            var idlist = ids.Split(',');
            int affectedRows = 0;
            foreach (var id in idlist)
            {
                affectedRows += await _repository.DeleteStepAsync(Convert.ToInt32(id));
            }
            
            if (affectedRows > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }
        #endregion




        #region HttpRequestProcessorStepDataHandler
        /// <summary>
        /// 创建数据处理器DataHandler
        /// </summary>
        /// <param name="dataHandler"></param>
        /// <returns></returns>
        public async Task<IActionResult> AddHttpRequestProcessorStepDataHandlerAsync([FromBody] HttpRequestProcessorStepDataHandlerCreateDto dataHandler)
        {
            if (dataHandler == null)
            {
                return Json(new OperationResult(false, "数据处理器不能为空"));
            }
            if (string.IsNullOrWhiteSpace(dataHandler.DataHandler))
            {
                return Json(new OperationResult(false, "数据处理器字段不能为空"));
            }
            if (string.IsNullOrWhiteSpace(dataHandler.ParametersInput))
            {
                return Json(new OperationResult(false, "数据处理器输入参数不能为空"));
            }
            if (dataHandler.StepId == 0)
            {
                return Json(new OperationResult(false, "所属步骤不能为空"));
            }
            var result = await _repository.AddDataHandlerAsync(dataHandler);
            if (result > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return BadRequest(result);
        }
        /// <summary>
        /// 更新数据处理器DataHandler
        /// </summary>
        /// <param name="dataHandler"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateHttpRequestProcessorStepDataHandlerAsync([FromBody] HttpRequestProcessorStepDataHandler dataHandler)
        {
            if (dataHandler == null)
            {
                return Json(new OperationResult(false, "数据处理器不能为空"));
            }
            if (string.IsNullOrWhiteSpace(dataHandler.DataHandler))
            {
                return Json(new OperationResult(false, "数据处理器字段不能为空"));
            }
            if (string.IsNullOrWhiteSpace(dataHandler.ParametersInput))
            {
                return Json(new OperationResult(false, "数据处理器输入参数不能为空"));
            }
            if (dataHandler.StepId == 0)
            {
                return Json(new OperationResult(false, "所属步骤不能为空"));
            }
            var dbStepDataHandler = await _repository.GetDataHandlerByIdAsync(dataHandler.Id);
            if (dbStepDataHandler is null)
            {
                return NotFound("Http处理器步骤不存在");
            }

            int affectedRows = await _repository.UpdateDataHandlerAsync(dataHandler);
            if (affectedRows > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }
        /// <summary>
        /// 删除数据处理器DataHandler
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public async Task<IActionResult> DeleteHttpRequestProcessorStepDataHandlerAsync([FromBody] string ids)
        {
            var idlist = ids.Split(',');
            int affectedRows = 0;
            foreach (var id in idlist)
            {
                affectedRows += await _repository.DeleteDataHandlerAsync(Convert.ToInt32(id));
            }

            if (affectedRows > 0)
            {
                return Ok(new OperationResult(true, string.Empty));
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }
        #endregion
    }
}

using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.RequestProcessor;
using Sylas.RemoteTasks.App.RequestProcessor.Models;
using Sylas.RemoteTasks.App.RequestProcessor.Models.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Dto;

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

        public IActionResult Index([FromServices] RequestProcessorService service)
        {
            return View();
        }

        public async Task<IActionResult> ExecuteHttpProcessorAsync([FromServices] RequestProcessorService service, [FromBody] ProcessorExecuteDto dto)
        {
            //await service.ExecuteFromAppsettingsAsync();
            if (dto is null || dto.ProcessorIds is null)
            {
                return Ok(new OperationResult(false, "参数不能为空"));
            }
            await service.ExecuteHttpRequestProcessorsAsync(dto.ProcessorIds, dto.StepId);
            return Ok(new OperationResult(true));
        }

        public async Task<IActionResult> GetHttpRequestProcessorsAsync(int pageIndex, int pageSize, string orderField, bool isAsc, [FromBody] DataFilter dataFilter)
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
                return Ok(new OperationResult(true, string.Empty) { Data = new string[] { $"{affectedRows}" } });
            }
            return Ok(new OperationResult(false, "数据没有变化"));
        }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> CloneProcessorAsync([FromBody] int id)
        {
            if (id == 0)
            {
                return BadRequest("克隆对象不能为空");
            }
            var clonedProcessorId = await _repository.CloneAsync(id);
            return Ok(new OperationResult(clonedProcessorId > 0, ""));
        }
        #endregion

        #region HttpRequestProcessorStep
        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> CloneStepAsync([FromBody] int id)
        {
            if (id == 0)
            {
                return BadRequest("克隆对象不能为空");
            }
            var clonedStepId = await _repository.CloneStepAsync(id);
            return Ok(new OperationResult(clonedStepId > 0, ""));
        }
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
            if (step.ProcessorId == 0)
            {
                return Json(new OperationResult(false, "所属HTTP处理器不能为空"));
            }

            // Step排序递增策略(实现默认按添加顺序执行)
            if (step.OrderNo == 0)
            {
                var processor = await _repository.GetByIdAsync(step.ProcessorId) ?? throw new Exception($"没有找到步骤所属的Http处理器");
                var lastStep = processor.Steps.OrderBy(x => x.OrderNo).LastOrDefault();
                if (lastStep is not null)
                {
                    step.OrderNo = lastStep.OrderNo + 10;
                }
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
            if (step.ProcessorId == 0)
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

            // DataHandler排序递增策略(实现默认按添加顺序执行)
            if (dataHandler.OrderNo == 0)
            {
                var step = await _repository.GetStepByIdAsync(dataHandler.StepId) ?? throw new Exception($"没有找到数据处理器所属步骤");
                var lastStep = step.DataHandlers.OrderBy(x => x.OrderNo).LastOrDefault();
                if (lastStep is not null)
                {
                    dataHandler.OrderNo = lastStep.OrderNo + 10;
                }
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

        public async Task<IActionResult> SyncDbs(string sourceConnectionString, string sourceDatabase, string sourceTable, string targetConnectionString)
        {
            sourceTable ??= "";
            if (!string.IsNullOrWhiteSpace($"{sourceConnectionString}{sourceDatabase}{sourceTable}{targetConnectionString}"))
            {
                if (string.IsNullOrWhiteSpace(sourceConnectionString) || string.IsNullOrWhiteSpace(targetConnectionString))
                {
                    ViewBag.Message = "源和目标连接字符串不能为空";
                }
                else
                {
                    foreach (var t in sourceTable.Split(','))
                    {
                        await DatabaseInfo.SyncDatabaseByConnectionStringsAsync(sourceConnectionString, targetConnectionString, sourceDatabase, t);
                    }
                    ViewBag.Message = "同步成功";
                }
            }
            return View();
        }
    }
}

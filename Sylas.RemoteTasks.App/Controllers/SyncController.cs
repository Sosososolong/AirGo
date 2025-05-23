using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.RequestProcessor;
using Sylas.RemoteTasks.App.RequestProcessor.Models;
using Sylas.RemoteTasks.App.RequestProcessor.Models.Dtos;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Dtos;
using Sylas.RemoteTasks.Database.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using System;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class SyncController(IHttpClientFactory httpClientFactory, ILogger<SyncController> logger, IConfiguration configuration, HttpRequestProcessorRepository repository) : CustomBaseController
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
        private readonly ILogger<SyncController> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly HttpRequestProcessorRepository _repository = repository;

        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> ExecuteHttpProcessorAsync([FromServices] RequestProcessorService service, [FromBody] ProcessorExecuteDto dto)
        {
            if (dto is null || dto.ProcessorIds is null)
            {
                return Ok(new OperationResult(false, "参数不能为空"));
            }
            await service.ExecuteHttpRequestProcessorsAsync(dto.ProcessorIds, dto.StepId);
            return Ok(RequestResult<bool>.Success(true));
        }

        /// <summary>
        /// 获取Http处理器
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetHttpRequestProcessorsAsync([FromBody] DataSearch search)
        {
            var processors = await _repository.GetPageAsync(search);
            var result = new RequestResult<PagedData<HttpRequestProcessor>>(processors);
            return Json(result);
        }

        public async Task<IActionResult> GetHttpRequestProcessorStepsAsync([FromBody] DataSearch search)
        {
            var steps = await _repository.GetStepsPageAsync(search);
            var result = new RequestResult<PagedData<HttpRequestProcessorStep>>(steps);
            return Json(result);
        }
        public async Task<IActionResult> GetHttpRequestProcessorStepDataHandlersAsync([FromBody] DataSearch search)
        {
            var steps = await _repository.GetDataHandlersPageAsync(search);
            var result = new RequestResult<PagedData<HttpRequestProcessorStepDataHandler>>(steps);
            return Json(result);
        }

        #region HttpRequestProcessor
        public async Task<IActionResult> AddHttpRequestProcessorAsync([FromBody] HttpRequestProcessorCreateDto processor)
        {
            if (processor == null)
            {
                return Json(RequestResult<bool>.Error("处理器不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Title))
            {
                return Json(RequestResult<bool>.Error("处理器标题不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Name))
            {
                return Json(RequestResult<bool>.Error("处理器名称/编码不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Url))
            {
                return Json(RequestResult<bool>.Error("处理器Url不能为空"));
            }
            var result = await _repository.AddAsync(processor);
            if (result > 0)
            {
                return Ok(RequestResult<bool>.Success(true));
            }
            return BadRequest(result);
        }
        public async Task<IActionResult> UpdateHttpRequestProcessorAsync([FromBody] HttpRequestProcessor processor)
        {
            if (processor == null)
            {
                return Json(RequestResult<bool>.Error("处理器不能为空"));
            }
            if (processor.Id == 0)
            {
                return Json(RequestResult<bool>.Error("处理器Id不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Title))
            {
                return Json(RequestResult<bool>.Error("处理器标题不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Name))
            {
                return Json(RequestResult<bool>.Error("处理器名称/编码不能为空"));
            }
            if (string.IsNullOrWhiteSpace(processor.Url))
            {
                return Json(RequestResult<bool>.Error("处理器Url不能为空"));
            }
            var dbProcessor = await _repository.GetByIdAsync(processor.Id);
            if (dbProcessor is null)
            {
                return NotFound("Http处理器不存在");
            }

            int affectedRows = await _repository.UpdateAsync(processor);
            if (affectedRows > 0)
            {
                return Ok(RequestResult<bool>.Success(true));
            }
            return Ok(RequestResult<bool>.Error("数据没有变化"));
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
                var result = RequestResult<string[]>.Success([$"{affectedRows}"]);
                return Ok(result);
            }
            return Ok(RequestResult<bool>.Error("数据没有变化"));
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
            return Ok(RequestResult<bool>.Success(clonedProcessorId > 0));
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

        [AllowAnonymous]
        public IActionResult SyncDbs()
        {
            return View();
        }
        public async Task<IActionResult> TransferAsync([FromServices] RepositoryBase<DbConnectionInfo> repository, [FromBody] TransferDataDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SourceConnectionString) || string.IsNullOrWhiteSpace(dto.SourceConnectionString))
            {
                return Ok(RequestResult<bool>.Error("源和目标连接字符串不能为空"));
            }
            else
            {
                var sourceConnInfo = await repository.GetByIdAsync(Convert.ToInt32(dto.SourceConnectionString));
                if (sourceConnInfo is null)
                {
                    return Ok(RequestResult<bool>.Error($"无效的数据库连接字符串: {dto.SourceConnectionString}"));
                }
                dto.SourceConnectionString = await SecurityHelper.AesDecryptAsync(sourceConnInfo.ConnectionString);
                
                var targetConnInfo = await repository.GetByIdAsync(Convert.ToInt32(dto.TargetConnectionString));
                if (targetConnInfo is null)
                {
                    return Ok(RequestResult<bool>.Error($"无效的数据库连接字符串: {dto.TargetConnectionString}"));
                }
                dto.TargetConnectionString = await SecurityHelper.AesDecryptAsync(targetConnInfo.ConnectionString);

                if (string.IsNullOrWhiteSpace($"{dto.SourceTable}{dto.TargetTable}"))
                {
                    await DatabaseInfo.TransferDataAsync(dto.SourceConnectionString, dto.TargetConnectionString, insertOnly: dto.InsertOnly);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(dto.TargetTable))
                    {
                        dto.TargetTable = dto.SourceTable;
                    }
                    string[] targetTables = dto.TargetTable.Split(',', ';');
                    int index = 0;
                    foreach (var t in dto.SourceTable.Split(',', ';'))
                    {
                        await DatabaseInfo.TransferDataAsync(dto.SourceConnectionString, dto.TargetConnectionString, sourceTable: t, targetTable: targetTables[index], insertOnly: dto.InsertOnly);
                        index++;
                    }
                }
                return Ok(RequestResult<bool>.Success(true));
            }
        }

        [AllowAnonymous]
        public IActionResult FromJson()
        {
            return View();
        }

        /// <summary>
        /// 从json文件同步数据到数据库中
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> SyncFromJsonsAsync([FromServices] RepositoryBase<DbConnectionInfo> dbConnRepository, [FromServices] IWebHostEnvironment env, [FromForm] SyncFromJsonsDto dto)
        {
            var uploadResult = await SaveUploadedFilesAsync(env);
            if (!uploadResult.Succeed)
            {
                return Ok(RequestResult<bool>.Error(uploadResult.Message));
            }
            var files = uploadResult.Data?.ToArray() ?? throw new Exception("文件上传异常");
            var tables = dto.TargetTables.Split(',', ';');

            var targetConnInfo = await dbConnRepository.GetByIdAsync(Convert.ToInt32(dto.TargetConnectionString));
            List<Task> tasks = [];
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileAbsolutePath = Path.Combine(env.WebRootPath, file);
                var json = await System.IO.File.ReadAllTextAsync(fileAbsolutePath);
                System.IO.File.Delete(fileAbsolutePath);
                
                var sourceRecords = JsonConvert.DeserializeObject<List<IDictionary<string, object>>>(json) ?? throw new Exception($"json文件{file}反序列化失败");

                if (targetConnInfo is null)
                {
                    return Ok(RequestResult<bool>.Error($"无效的数据库连接字符串: {dto.TargetConnectionString}"));
                }
                dto.TargetConnectionString = await SecurityHelper.AesDecryptAsync(targetConnInfo.ConnectionString);
                tasks.Add(DatabaseInfo.TransferDataAsync(sourceRecords, dto.TargetConnectionString, tables[i]));
            }
            await Task.WhenAll(tasks);
            return Ok(RequestResult<bool>.Success(true));
        }
    }
}

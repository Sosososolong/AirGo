using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.ApiTester.Repositories;
using Sylas.RemoteTasks.App.ApiTester.Services;

namespace Sylas.RemoteTasks.App.Controllers
{
    /// <summary>
    /// ApiTester 模块: HTTP 接口请求测试工具(复刻 Coze API Tester)
    /// </summary>
    public class ApiTesterController(
        ApiTesterRepository repository,
        ApiTesterService service,
        RequestProxyService proxy,
        ILogger<ApiTesterController> logger) : CustomBaseController
    {
        private readonly ApiTesterRepository _repository = repository;
        private readonly ApiTesterService _service = service;
        private readonly RequestProxyService _proxy = proxy;
        private readonly ILogger<ApiTesterController> _logger = logger;

        /// <summary>
        /// 主页面
        /// </summary>
        [AllowAnonymous]
        public IActionResult Index() => View();

        #region 集合
        [HttpPost]
        public async Task<IActionResult> GetCollections()
        {
            var page = await _service.GetCollectionsAsync();
            return Json(new { code = 0, data = page.Data, total = page.Count });
        }

        [HttpPost]
        public async Task<IActionResult> SaveCollection([FromBody] ApiCollectionSaveDto dto)
        {
            var id = await _service.SaveCollectionAsync(dto);
            return Json(new { code = 0, data = new { id } });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCollection([FromBody] IdDto dto)
        {
            var rows = await _service.DeleteCollectionAsync(dto.Id);
            return Json(new { code = 0, data = new { rows } });
        }
        #endregion

        #region 接口
        [HttpPost]
        public async Task<IActionResult> GetEndpoints([FromBody] CollectionIdDto dto)
        {
            var page = await _service.GetEndpointsAsync(dto.CollectionId);
            return Json(new { code = 0, data = page.Data, total = page.Count });
        }

        [HttpPost]
        public async Task<IActionResult> SaveEndpoint([FromBody] ApiEndpointSaveDto dto)
        {
            var id = await _service.SaveEndpointAsync(dto);
            return Json(new { code = 0, data = new { id } });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEndpoint([FromBody] IdDto dto)
        {
            var rows = await _service.DeleteEndpointAsync(dto.Id);
            return Json(new { code = 0, data = new { rows } });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEndpointsOrder([FromBody] EndpointsOrderDto dto)
        {
            var rows = await _service.UpdateEndpointsOrderAsync(dto);
            return Json(new { code = 0, data = new { rows } });
        }

        [HttpPost]
        public async Task<IActionResult> GetEndpoint([FromBody] IdDto dto)
        {
            var ep = await _repository.Endpoints.GetByIdAsync(dto.Id);
            if (ep is null) return Json(new { code = 1, msg = "接口不存在" });
            ApiCollection? col = ep.CollectionId > 0 ? await _repository.Collections.GetByIdAsync(ep.CollectionId) : null;
            return Json(new { code = 0, data = new { endpoint = ep, collection = col } });
        }
        #endregion

        #region 发送请求
        [HttpPost]
        public async Task<IActionResult> SendRequest([FromBody] SendRequestDto dto)
        {
            try
            {
                var result = await _proxy.SendAsync(dto);
                return Json(new { code = 0, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送请求失败");
                return Json(new { code = 1, msg = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BatchSend([FromBody] BatchSendDto dto)
        {
            try
            {
                var result = await _proxy.BatchSendAsync(dto);
                return Json(new { code = 0, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量发送失败");
                return Json(new { code = 1, msg = ex.Message });
            }
        }
        #endregion

        #region 环境与变量
        [HttpPost]
        public async Task<IActionResult> GetEnvironments()
        {
            var page = await _service.GetEnvironmentsAsync();
            return Json(new { code = 0, data = page.Data, total = page.Count });
        }

        [HttpPost]
        public async Task<IActionResult> SaveEnvironment([FromBody] ApiEnvironmentSaveDto dto)
        {
            var id = await _service.SaveEnvironmentAsync(dto);
            return Json(new { code = 0, data = new { id } });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEnvironment([FromBody] IdDto dto)
        {
            var rows = await _service.DeleteEnvironmentAsync(dto.Id);
            return Json(new { code = 0, data = new { rows } });
        }

        [HttpPost]
        public async Task<IActionResult> SetActiveEnvironment([FromBody] IdDto dto)
        {
            var rows = await _service.SetActiveEnvironmentAsync(dto.Id);
            return Json(new { code = 0, data = new { rows } });
        }

        [HttpPost]
        public async Task<IActionResult> GetVariables([FromBody] IdDto dto)
        {
            // dto.Id = environmentId
            var page = await _service.GetVariablesAsync(dto.Id);
            return Json(new { code = 0, data = page.Data, total = page.Count });
        }

        [HttpPost]
        public async Task<IActionResult> SaveVariable([FromBody] ApiVariableSaveDto dto)
        {
            var id = await _service.SaveVariableAsync(dto);
            return Json(new { code = 0, data = new { id } });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteVariable([FromBody] IdDto dto)
        {
            var rows = await _service.DeleteVariableAsync(dto.Id);
            return Json(new { code = 0, data = new { rows } });
        }
        #endregion

        #region Swagger 导入
        [HttpPost]
        public async Task<IActionResult> ImportSwagger([FromBody] SwaggerImportDto dto)
        {
            try
            {
                var collectionId = await _service.ImportSwaggerAsync(dto);
                return Json(new { code = 0, data = new { collectionId } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Swagger 导入失败");
                return Json(new { code = 1, msg = ex.Message });
            }
        }
        #endregion

        #region 导入导出 JSON
        [HttpPost]
        public async Task<IActionResult> Export([FromBody] IdDto dto)
        {
            try
            {
                var data = await _service.ExportCollectionAsync(dto.Id);
                return Json(new { code = 0, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出集合失败");
                return Json(new { code = 1, msg = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportJson([FromBody] ImportJsonDto dto)
        {
            try
            {
                var collectionId = await _service.ImportJsonAsync(dto.Content ?? string.Empty);
                return Json(new { code = 0, data = new { collectionId } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入 JSON 失败");
                return Json(new { code = 1, msg = ex.Message });
            }
        }
        #endregion
    }
}

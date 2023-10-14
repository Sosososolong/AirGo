using Org.BouncyCastle.Crypto;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos;
using System.Drawing.Printing;
using System.Text;

namespace Sylas.RemoteTasks.App.Repositories
{
    public class HttpRequestProcessorRepository
    {
        private readonly IDatabaseProvider _db;

        public HttpRequestProcessorRepository(IDatabaseProvider databaseProvider)
        {
            _db = databaseProvider;
        }

        #region HttpRequestProcessors
        /// <summary>
        /// 分页查询多个Http请求处理器
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<PagedData<HttpRequestProcessor>> GetPageAsync(int pageIndex, int pageSize, string orderField, bool isAsc, DataFilter filter)
        {
            var pages = await _db.QueryPagedDataAsync<HttpRequestProcessor>(HttpRequestProcessor.TableName, pageIndex, pageSize, orderField, isAsc, filter);
            foreach (var processor in pages.Data)
            {
                var stepsFilters = new List<FilterItem>
                {
                    new FilterItem { FieldName = "processorId", CompareType = "=", Value = processor.Id.ToString() }
                };
                var steps = (await GetStepsPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = stepsFilters })).Data;
                processor.Steps = steps;

                foreach (var step in steps)
                {
                    var filterCondition = new FilterItem { FieldName = "StepId", CompareType = "=", Value = step.Id.ToString() };
                    var dataHandlers = (await GetDataHandlersPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = new List<FilterItem> { filterCondition } })).Data;
                    step.DataHandlers = dataHandlers;
                }
            }
            return pages;
        }
        /// <summary>
        /// 根据Id查询Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<HttpRequestProcessor?> GetByIdAsync(int id)
        {
            var pages = await _db.QueryPagedDataAsync<HttpRequestProcessor>(HttpRequestProcessor.TableName, 1, 1, "id", true, new DataFilter { FilterItems = new List<FilterItem> { new FilterItem { CompareType = "=", FieldName = "id", Value = id.ToString() } } });
            var processor = pages.Data.FirstOrDefault();
            if (processor is null)
            {
                return null;
            }
            var stepsFilters = new List<FilterItem>
                {
                    new FilterItem { FieldName = "processorId", CompareType = "=", Value = processor.Id.ToString() }
                };
            var steps = (await GetStepsPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = stepsFilters })).Data;
            processor.Steps = steps;

            foreach (var step in steps)
            {
                var filterCondition = new FilterItem { FieldName = "StepId", CompareType = "=", Value = step.Id.ToString() };
                var dataHandlers = (await GetDataHandlersPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = new List<FilterItem> { filterCondition } })).Data;
                step.DataHandlers = dataHandlers;
            }
            return processor;
        }
        /// <summary>
        /// 添加一个新的Http请求处理器
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        public async Task<int> AddAsync(HttpRequestProcessorCreateDto processor)
        {
            string sql = $"insert into {HttpRequestProcessor.TableName} (Name, Title, Url, Headers, Remark, StepCirleRunningWhenLastStepHasData) values(@Name, @Title, @Url, @Headers, @Remark, @StepCirleRunningWhenLastStepHasData);SELECT last_insert_rowid();";
            var parameters = new Dictionary<string, object>
            {
                { "Name", processor.Name },
                { "Title", processor.Title },
                { "Url", processor.Url },
                { "Headers", processor.Headers },
                { "Remark", processor.Remark },
                { "StepCirleRunningWhenLastStepHasData", processor.StepCirleRunningWhenLastStepHasData }
            };
            return await _db.ExecuteScalarAsync(sql, parameters);
        }
        /// <summary>
        /// 更新Http请求处理器
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> UpdateAsync(HttpRequestProcessor processor)
        {
            var parameters = new Dictionary<string, object> { { "id", processor.Id } };

            var recordCount = await _db.ExecuteSqlAsync($"select count(*) from {HttpRequestProcessor.TableName} where id=@id", parameters);
            if (recordCount == 0)
            {
                throw new Exception("HttpRequestProcerssor不存在");
            }

            StringBuilder setStatement = new();
            if (!string.IsNullOrWhiteSpace(processor.Title))
            {
                setStatement.Append($"Title=@Title,");
                parameters.Add("Title", processor.Title);
            }
            if (!string.IsNullOrWhiteSpace(processor.Name))
            {
                setStatement.Append($"name=@name,");
                parameters.Add("name", processor.Name);
            }
            if (!string.IsNullOrWhiteSpace(processor.Url))
            {
                setStatement.Append($"url=@url,");
                parameters.Add("url", processor.Url);
            }
            if (!string.IsNullOrWhiteSpace(processor.Headers))
            {
                setStatement.Append($"Headers=@Headers,");
                parameters.Add("Headers", processor.Headers);
            }
            if (!string.IsNullOrWhiteSpace(processor.Remark))
            {
                setStatement.Append($"remark=@remark,");
                parameters.Add("remark", processor.Remark);
            }
            setStatement.Append($"stepCirleRunningWhenLastStepHasData=@stepCirleRunningWhenLastStepHasData,");
            parameters.Add("stepCirleRunningWhenLastStepHasData", processor.StepCirleRunningWhenLastStepHasData);

            string sql = $"update {HttpRequestProcessor.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 删除Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteAsync(int id)
        {
            var deleted = await _db.ExecuteSqlsAsync(new string[] {
                $"DELETE FROM {HttpRequestProcessorStepDataHandler.TableName} WHERE StepId IN(SELECT Id FROM HttpRequestProcessorSteps WHERE ProcessorId=@id)",
                $"DELETE FROM {HttpRequestProcessorStep.TableName} WHERE ProcessorId=@id",
                $"DELETE FROM {HttpRequestProcessor.TableName} WHERE Id=@id",
            },
            new Dictionary<string, object> { { "id", id } });
            return deleted;
        }

        public async Task<int> CloneAsync(int id)
        {
            var processor = await GetByIdAsync(id) ?? throw new Exception($"未找到指定的HTTP处理器{id}");
            var clonedProcessorId = await AddAsync(processor.ToCreateDto());
            if (clonedProcessorId <= 0)
            {
                return 0;
            }
            foreach (var step in processor.Steps)
            {
                step.ProcessorId = clonedProcessorId;
                var clonedStepId = await AddStepAsync(step.ToCreateDto());
                foreach (var dataHandler in step.DataHandlers)
                {
                    dataHandler.StepId = clonedStepId;
                    _ = await AddDataHandlerAsync(dataHandler.ToCreateDto());
                }
            }
            return clonedProcessorId;
        }
        #endregion

        #region HttpRequestProcessorSteps
        /// <summary>
        /// 分页查询Http请求处理器具体执行的多个步骤
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<PagedData<HttpRequestProcessorStep>> GetStepsPageAsync(int pageIndex, int pageSize, string orderField, bool isAsc, DataFilter filter)
        {
            return await _db.QueryPagedDataAsync<HttpRequestProcessorStep>(HttpRequestProcessorStep.TableName, pageIndex, pageSize, orderField, isAsc, filter);
        }

        public async Task<int> CloneStepAsync(int id)
        {
            var step = await GetStepByIdAsync(id) ?? throw new Exception($"未找到指定的Step:{id}");
            var clonedStepId = await AddStepAsync(step.ToCreateDto());
            if (clonedStepId <= 0)
            {
                return 0;
            }
            foreach (var handler in step.DataHandlers)
            {
                handler.StepId = clonedStepId;
                _ = await AddDataHandlerAsync(handler.ToCreateDto());
            }
            return clonedStepId;
        }
        public async Task<HttpRequestProcessorStep?> GetStepByIdAsync(int id)
        {
            var pages = await _db.QueryPagedDataAsync<HttpRequestProcessorStep>(HttpRequestProcessorStep.TableName, 1, 1, "id", true, new DataFilter { FilterItems = new List<FilterItem> { new FilterItem { CompareType = "=", FieldName = "id", Value = id.ToString() } } });
            var step = pages.Data.FirstOrDefault();
            if (step is null)
            {
                return null;
            }
            var dataHandlersFilters = new List<FilterItem>
                {
                    new FilterItem { FieldName = "stepId", CompareType = "=", Value = step.Id.ToString() }
                };
            var dataHandlers = (await GetDataHandlersPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = dataHandlersFilters })).Data;
            step.DataHandlers = dataHandlers;
            return step;
        }
        /// <summary>
        /// 添加一个新的Http请求处理器
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        public async Task<int> AddStepAsync(HttpRequestProcessorStepCreateDto step)
        {
            string sql = $"insert into {HttpRequestProcessorStep.TableName} (parameters, RequestBody, dataContextBuilder, remark, processorId, presetDataContext) values(@parameters, @RequestBody, @dataContextBuilder, @remark, @processorId, @presetDataContext);SELECT last_insert_rowid();";
            var parameters = new Dictionary<string, object>
            {
                { "parameters", step.Parameters },
                { "RequestBody", step.RequestBody },
                { "dataContextBuilder", step.DataContextBuilder },
                { "remark", step.Remark },
                { "processorId", step.ProcessorId },
                { "presetDataContext", step.PresetDataContext },
            };
            return await _db.ExecuteScalarAsync(sql, parameters);
        }
        /// <summary>
        /// 更新Http请求处理器
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> UpdateStepAsync(HttpRequestProcessorStep step)
        {
            var parameters = new Dictionary<string, object> { { "id", step.Id } };

            var recordCount = await _db.ExecuteSqlAsync($"select count(*) from {HttpRequestProcessorStep.TableName} where id=@id", parameters);
            if (recordCount == 0)
            {
                throw new Exception("要更新的HttpRequestProcerssorStep不存在");
            }

            StringBuilder setStatement = new();
            setStatement.Append($"parameters=@parameters,");
            parameters.Add("parameters", step.Parameters);

            setStatement.Append($"RequestBody=@RequestBody,");
            parameters.Add("RequestBody", step.RequestBody);

            setStatement.Append($"PresetDataContext=@PresetDataContext,");
            parameters.Add("PresetDataContext", step.PresetDataContext);

            setStatement.Append($"dataContextBuilder=@dataContextBuilder,");
            parameters.Add("dataContextBuilder", step.DataContextBuilder);

            setStatement.Append($"remark=@remark,");
            parameters.Add("remark", step.Remark ?? "");

            setStatement.Append($"EndDataContext=@EndDataContext,");
            parameters.Add("EndDataContext", step.EndDataContext ?? "");

            setStatement.Append($"OrderNo=@OrderNo,");
            parameters.Add("OrderNo", step.OrderNo);

            if (step.ProcessorId > 0)
            {
                setStatement.Append($"processorId=@processorId,");
                parameters.Add("processorId", step.ProcessorId);
            }
            string sql = $"update {HttpRequestProcessorStep.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 删除Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteStepAsync(int id)
        {
            string sql = $"delete from {HttpRequestProcessorStep.TableName} where id=@id";
            int count = await _db.ExecuteSqlAsync(sql, new Dictionary<string, object> { { "id", id } });

            string deleteDataHandlers = $"delete from {HttpRequestProcessorStepDataHandler.TableName} where {nameof(HttpRequestProcessorStepDataHandler.StepId)}=@id";
            count += await _db.ExecuteSqlAsync(deleteDataHandlers, new Dictionary<string, object> { { "id", id } });

            return count;
        }
        #endregion
        
        #region HttpRequestProcessorStepDataHandlers
        /// <summary>
        /// 分页查询Http请求处理器具体执行的多个步骤
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<PagedData<HttpRequestProcessorStepDataHandler>> GetDataHandlersPageAsync(int pageIndex, int pageSize, string orderField, bool isAsc, DataFilter filter)
        {
            return await _db.QueryPagedDataAsync<HttpRequestProcessorStepDataHandler>(HttpRequestProcessorStepDataHandler.TableName, pageIndex, pageSize, orderField, isAsc, filter);
        }
        /// <summary>
        /// 根据Id查询数据处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<HttpRequestProcessorStepDataHandler?> GetDataHandlerByIdAsync(int id)
        {
            var pages = await _db.QueryPagedDataAsync<HttpRequestProcessorStepDataHandler>(HttpRequestProcessorStepDataHandler.TableName, 1, 1, "id", true, new DataFilter { FilterItems = new List<FilterItem> { new FilterItem { CompareType = "=", FieldName = "id", Value = id.ToString() } } });
            var dataHandler = pages.Data.FirstOrDefault();
            return dataHandler;
        }
        /// <summary>
        /// 添加一个新的Http请求处理器
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        public async Task<int> AddDataHandlerAsync(HttpRequestProcessorStepDataHandlerCreateDto dataHandler)
        {
            string sql = $"insert into {HttpRequestProcessorStepDataHandler.TableName} (DataHandler, ParametersInput, Remark, StepId, OrderNo, Enabled) values(@dataHandler, @parametersInput, @remark, @stepId, @OrderNo, @enabled)";
            var parameters = new Dictionary<string, object>
            {
                { "dataHandler", dataHandler.DataHandler },
                { "parametersInput" , dataHandler.ParametersInput },
                { "remark", dataHandler.Remark },
                { "stepId" , dataHandler.StepId },
                { "OrderNo" , dataHandler.OrderNo },
                { "enabled" , dataHandler.Enabled ? 1 : 0 }
            };
            return await _db.ExecuteSqlAsync(sql, parameters);
        }
        /// <summary>
        /// 更新Http请求处理器
        /// </summary>
        /// <param name="dataHandler"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> UpdateDataHandlerAsync(HttpRequestProcessorStepDataHandler dataHandler)
        {
            var parameters = new Dictionary<string, object> { { "id", dataHandler.Id } };

            var recordCount = await _db.ExecuteSqlAsync($"select count(*) from {HttpRequestProcessorStepDataHandler.TableName} where id=@id", parameters);
            if (recordCount == 0)
            {
                throw new Exception("要更新的HttpRequestProcerssorStep不存在");
            }

            StringBuilder setStatement = new();
            if (!string.IsNullOrWhiteSpace(dataHandler.DataHandler))
            {
                setStatement.Append($"dataHandler=@dataHandler,");
                parameters.Add("dataHandler", dataHandler.DataHandler);
            }
            if (!string.IsNullOrWhiteSpace(dataHandler.ParametersInput))
            {
                setStatement.Append($"parametersInput=@parametersInput,");
                parameters.Add("parametersInput", dataHandler.ParametersInput);
            }
            if (!string.IsNullOrWhiteSpace(dataHandler.Remark))
            {
                setStatement.Append($"remark=@remark,");
                parameters.Add("remark", dataHandler.Remark);
            }
            if (dataHandler.StepId > 0)
            {
                setStatement.Append($"stepId=@stepId,");
                parameters.Add("stepId", dataHandler.StepId);
            }
            setStatement.Append($"OrderNo=@OrderNo,");
            parameters.Add("OrderNo", dataHandler.OrderNo);
            setStatement.Append($"Enabled=@Enabled,");
            parameters.Add("Enabled", dataHandler.Enabled ? 1 : 0);
            string sql = $"update {HttpRequestProcessorStepDataHandler.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 删除Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteDataHandlerAsync(int id)
        {
            string sql = $"delete from {HttpRequestProcessorStepDataHandler.TableName} where id=@id";
            return await _db.ExecuteSqlAsync(sql, new Dictionary<string, object> { { "id", id } });
        }
        #endregion
    }
}

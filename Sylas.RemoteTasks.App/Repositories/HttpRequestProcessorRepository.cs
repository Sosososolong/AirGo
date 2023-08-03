using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos;
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
                    new FilterItem { FieldName = "httpProcessorId", CompareType = "=", Value = processor.Id.ToString() }
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
                    new FilterItem { FieldName = "httpProcessorId", CompareType = "=", Value = processor.Id.ToString() }
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
            string sql = $"insert into {HttpRequestProcessor.TableName} (Name, Title, Url, Remark, StepCirleRunningWhenLastStepHasData) values(@name, @title, @url, @remark, @stepCirleRunningWhenLastStepHasData)";
            var parameters = new Dictionary<string, object>
            {
                { "name", processor.Name },
                { "title", processor.Title },
                { "url", processor.Url },
                { "remark", processor.Remark },
                { "stepCirleRunningWhenLastStepHasData", processor.StepCirleRunningWhenLastStepHasData }
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

            var recordCount = await _db.ExecuteScalarAsync($"select count(*) from {HttpRequestProcessor.TableName} where id=@id", parameters);
            if (recordCount == 0)
            {
                throw new Exception("HttpRequestProcerssor不存在");
            }

            StringBuilder setStatement = new();
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
            if (!string.IsNullOrWhiteSpace(processor.Remark))
            {
                setStatement.Append($"remark=@remark,");
                parameters.Add("remark", processor.Remark);
            }
            setStatement.Append($"stepCirleRunningWhenLastStepHasData=@stepCirleRunningWhenLastStepHasData,");
            parameters.Add("stepCirleRunningWhenLastStepHasData", processor.StepCirleRunningWhenLastStepHasData);

            string sql = $"update {HttpRequestProcessor.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteScalarAsync(sql, parameters);
        }

        /// <summary>
        /// 删除Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteAsync(int id)
        {
            string sql = $"delete from {HttpRequestProcessor.TableName} where id=@id";
            return await _db.ExecuteScalarAsync(sql, new Dictionary<string, object> { { "id", id } });
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
        /// <summary>
        /// 添加一个新的Http请求处理器
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        public async Task<int> AddStepAsync(HttpRequestProcessorStep step)
        {
            string sql = $"insert into {HttpRequestProcessorStep.TableName} values(@parameters, @dataContextBuilder, @remark, @processorId)";
            var parameters = new Dictionary<string, object>
            {
                { "parameters", step.Parameters },
                { "dataContextBuilder", step.DataContextBuilder },
                { "remark", step.Remark },
                { "processorId", step.HttpRequestProcessorId }
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

            var recordCount = await _db.ExecuteScalarAsync($"select count(*) from {HttpRequestProcessorStep.TableName} where id=@id", parameters);
            if (recordCount == 0)
            {
                throw new Exception("要更新的HttpRequestProcerssorStep不存在");
            }

            StringBuilder setStatement = new();
            if (!string.IsNullOrWhiteSpace(step.Parameters))
            {
                setStatement.Append($"parameters=@parameters,");
                parameters.Add("parameters", step.Parameters);
            }
            if (!string.IsNullOrWhiteSpace(step.DataContextBuilder))
            {
                setStatement.Append($"dataContextBuilder=@dataContextBuilder,");
                parameters.Add("dataContextBuilder", step.DataContextBuilder);
            }
            if (!string.IsNullOrWhiteSpace(step.Remark))
            {
                setStatement.Append($"remark=@remark,");
                parameters.Add("remark", step.Remark);
            }
            if (step.HttpRequestProcessorId > 0)
            {
                setStatement.Append($"processorId=@processorId,");
                parameters.Add("processorId", step.HttpRequestProcessorId);
            }
            string sql = $"update {HttpRequestProcessorStep.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteScalarAsync(sql, parameters);
        }

        /// <summary>
        /// 删除Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteStepAsync(int id)
        {
            string sql = $"delete from {HttpRequestProcessorStep.TableName} where id=@id";
            return await _db.ExecuteScalarAsync(sql, new Dictionary<string, object> { { "id", id } });
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
        /// 添加一个新的Http请求处理器
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        public async Task<int> AddDataHandlerAsync(HttpRequestProcessorStepDataHandler dataHandler)
        {
            string sql = $"insert into {HttpRequestProcessorStepDataHandler.TableName} values(@dataHandler, @parametersInput, @remark, @stepId)";
            var parameters = new Dictionary<string, object>
            {
                { "dataHandler", dataHandler.DataHandler },
                { "parametersInput" , dataHandler.ParametersInput },
                { "remark", dataHandler.Remark },
                { "stepId" , dataHandler.StepId }
            };
            return await _db.ExecuteScalarAsync(sql, parameters);
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

            var recordCount = await _db.ExecuteScalarAsync($"select count(*) from {HttpRequestProcessorStepDataHandler.TableName} where id=@id", parameters);
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
            string sql = $"update {HttpRequestProcessorStepDataHandler.TableName} set {setStatement.ToString().TrimEnd(',')} where id=@id";
            return await _db.ExecuteScalarAsync(sql, parameters);
        }

        /// <summary>
        /// 删除Http请求处理器
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteDataHandlerAsync(int id)
        {
            string sql = $"delete from {HttpRequestProcessorStepDataHandler.TableName} where id=@id";
            return await _db.ExecuteScalarAsync(sql, new Dictionary<string, object> { { "id", id } });
        }
        #endregion
    }
}

using Sylas.RemoteTasks.App.Repositories;

namespace Sylas.RemoteTasks.App.Models.HttpRequestProcessor
{
    public class HttpRequestProcessorStep
    {
        public const string TableName = "HttpRequestProcessorSteps";
        public int Id { get; set; }
        public string Parameters { get; set; } = string.Empty;
        public string DataContextBuilder { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int HttpRequestProcessorId { get; set; }
        // ["$dataModelCodes=v_dszs_dslx_tjysjl", "$param2=value2"]
        public string PresetDataContext { get; set; } = string.Empty;
        public IEnumerable<HttpRequestProcessorStepDataHandler> DataHandlers { get; set; } = new List<HttpRequestProcessorStepDataHandler>();
    }
}

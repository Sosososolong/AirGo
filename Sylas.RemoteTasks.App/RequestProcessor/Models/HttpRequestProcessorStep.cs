namespace Sylas.RemoteTasks.App.RequestProcessor.Models
{
    public class HttpRequestProcessorStep
    {
        public const string TableName = "HttpRequestProcessorSteps";
        public int Id { get; set; }
        public string Parameters { get; set; } = string.Empty;
        public string RequestBody { get; set; } = string.Empty;
        public string DataContextBuilder { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int ProcessorId { get; set; }
        // ["$dataModelCodes=v_dszs_dslx_tjysjl", "$param2=value2"]
        public string PresetDataContext { get; set; } = string.Empty;
        public string EndDataContext { get; set; } = string.Empty;
        public int OrderNo { get; set; }
        public IEnumerable<HttpRequestProcessorStepDataHandler> DataHandlers { get; set; } = new List<HttpRequestProcessorStepDataHandler>();
    }
}

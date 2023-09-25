namespace Sylas.RemoteTasks.App.Models.HttpRequestProcessor
{
    public class HttpRequestProcessorStepDataHandler
    {
        public const string TableName = "HttpRequestProcessorStepDataHandlers";
        public int Id { get; set; }
        public string DataHandler { get; set; } = string.Empty;
        public string ParametersInput { get; set; } = string.Empty;
        public int StepId { get; set; }
        public string Remark { get; set; } = string.Empty;
        public int OrderNo { get; set; }
        public bool Enabled { get; set; }
    }
}

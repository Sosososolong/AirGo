namespace Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos
{
    public class HttpRequestProcessorStepDataHandlerCreateDto
    {
        public string DataHandler { get; set; } = string.Empty;
        public string ParametersInput { get; set; } = string.Empty;
        public int StepId { get; set; }
        public string Remark { get; set; } = string.Empty;
    }
}

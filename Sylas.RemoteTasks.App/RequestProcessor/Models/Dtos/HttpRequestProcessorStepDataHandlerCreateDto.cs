namespace Sylas.RemoteTasks.App.RequestProcessor.Models.Dtos
{
    public class HttpRequestProcessorStepDataHandlerCreateDto
    {
        public string DataHandler { get; set; } = string.Empty;
        public string ParametersInput { get; set; } = string.Empty;
        public int StepId { get; set; }
        public int OrderNo { get; set; }
        public bool Enabled { get; set; } = true;
        public string Remark { get; set; } = string.Empty;
    }
}

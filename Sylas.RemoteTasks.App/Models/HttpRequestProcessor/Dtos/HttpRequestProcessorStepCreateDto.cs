namespace Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos
{
    public class HttpRequestProcessorStepCreateDto
    {
        public string Parameters { get; set; } = string.Empty;
        public string RequestBody { get; set; } = string.Empty;
        public string DataContextBuilder { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int ProcessorId { get; set; }
        public int OrderNo { get; set; }
        // ["$dataModelCodes=v_dszs_dslx_tjysjl", "$param2=value2"]
        public string PresetDataContext { get; set; } = string.Empty;
    }
}

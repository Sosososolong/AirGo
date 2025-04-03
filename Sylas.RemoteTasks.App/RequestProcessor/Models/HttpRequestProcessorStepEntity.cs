using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.RequestProcessor.Models
{
    [Table(TableName)]
    public class HttpRequestProcessorStepEntity : EntityBase<int>
    {
        public const string TableName = "HttpRequestProcessorSteps";
        public string Parameters { get; set; } = string.Empty;
        public string RequestBody { get; set; } = string.Empty;
        public string DataContextBuilder { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int ProcessorId { get; set; }
        // ["$dataModelCodes=v_dszs_dslx_tjysjl", "$param2=value2"]
        public string PresetDataContext { get; set; } = string.Empty;
        public string EndDataContext { get; set; } = string.Empty;
        public int OrderNo { get; set; }
    }
}

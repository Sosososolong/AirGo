namespace Sylas.RemoteTasks.App.Models.HttpRequestProcessor.Dtos
{
    public class ProcessorExecuteDto
    {
        public int[] ProcessorIds { get; set; } = { 0 };
        public int StepId { get; set; }
    }
}

namespace Sylas.RemoteTasks.App.RequestProcessor.Models.Dtos
{
    public class ProcessorExecuteDto
    {
        public int[] ProcessorIds { get; set; } = { 0 };
        public int StepId { get; set; }
    }
}

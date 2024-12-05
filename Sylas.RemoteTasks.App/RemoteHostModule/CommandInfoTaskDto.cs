namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class CommandInfoTaskDto : CommandInfoInDto
    {
        public string RequestId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }
}

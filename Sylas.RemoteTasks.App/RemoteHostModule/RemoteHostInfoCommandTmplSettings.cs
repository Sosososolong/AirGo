namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class RemoteHostInfoCommandSettings
    {
        public string Host { get; set; } = string.Empty;
        public List<RemoteHostInfoCommand> RemoteHostInfoCommands { get; set; }
    }
}

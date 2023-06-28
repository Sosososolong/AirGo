namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class RemoteHostInfoCommandSettings
    {
        public string Host { get; set; } = string.Empty;
        public List<CommandInfo> RemoteHostInfoCommands { get; set; } = new List<CommandInfo>();
    }
}

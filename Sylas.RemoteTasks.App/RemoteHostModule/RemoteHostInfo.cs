namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public abstract class RemoteHostInfo
    {
        public abstract string Description { get; }
        public abstract string Name { get; }
        public abstract List<Tuple<string, string>> Labels { get; }
        public abstract List<RemoteHostInfoCommand> Commands { get; set; }
    }
}

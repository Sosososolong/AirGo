namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public abstract class RemoteHostInfoProvider
    {
        protected RemoteHostInfoProvider(RemoteHost remoteHost, ContainerFactory remoteHostInfoFactory)
        {
            RemoteHost = remoteHost ?? throw new ArgumentNullException(nameof(remoteHost));
            RemoteHostInfoFactory = remoteHostInfoFactory ?? throw new ArgumentNullException(nameof(remoteHostInfoFactory));
        }
        public RemoteHost RemoteHost { get; set; }
        public ContainerFactory RemoteHostInfoFactory { get; set; }
        public abstract List<RemoteHostInfo> RemoteHostInfos();
    }
}

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public abstract class RemoteHostInfoProvider
    {
        protected RemoteHostInfoProvider(RemoteHost remoteHost, RemoteHostInfoFactory remoteHostInfoFactory)
        {
            RemoteHost = remoteHost ?? throw new ArgumentNullException(nameof(remoteHost));
            RemoteHostInfoFactory = remoteHostInfoFactory ?? throw new ArgumentNullException(nameof(remoteHostInfoFactory));
        }
        public RemoteHost RemoteHost { get; set; }
        public RemoteHostInfoFactory RemoteHostInfoFactory { get; set; }
        public abstract List<RemoteHostInfo> RemoteHostInfos();
    }
}

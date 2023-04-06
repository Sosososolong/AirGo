namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public abstract class RemoteHostInfoManager
    {
        protected RemoteHostInfoManager(RemoteHost remoteHost, RemoteHostInfoFactory remoteHostInfoFactory)
        {
            RemoteHost = remoteHost ?? throw new ArgumentNullException(nameof(remoteHost));
            RemoteHostInfoFactory = remoteHostInfoFactory ?? throw new ArgumentNullException(nameof(remoteHostInfoFactory));
        }
        public RemoteHost RemoteHost { get; set; }
        public RemoteHostInfoFactory RemoteHostInfoFactory { get; set; }
        public abstract List<RemoteHostInfo> RemoteHostInfos();
    }
}

using Microsoft.Extensions.Options;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public abstract class RemoteHostInfoProvider
    {
        protected RemoteHostInfoProvider(RemoteHost remoteHost)
        {
            RemoteHost = remoteHost;
        }
        public RemoteHost RemoteHost { get; set; }
        public abstract List<RemoteHostInfo> RemoteHostInfos();
    }
}

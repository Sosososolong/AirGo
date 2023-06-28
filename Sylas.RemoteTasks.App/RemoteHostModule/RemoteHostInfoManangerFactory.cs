namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// 需要即使动态创建的时候调用
    /// </summary>
    public class RemoteHostInfoManagerFactory
    {

        public RemoteHostInfoManager CreateDockerContainerInfo(RemoteHost remoteHost, RemoteHostInfoFactory remoteHostInfoFactory)
        {
            return new RemoteHostInfoMangerDockerContainer(remoteHost, remoteHostInfoFactory);
        }
    }
}

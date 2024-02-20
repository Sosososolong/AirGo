namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// 需要即使动态创建的时候调用
    /// </summary>
    public class RemoteHostInfoManagerFactory
    {

        public RemoteHostInfoProvider CreateDockerContainerInfo(RemoteHost remoteHost, ContainerFactory remoteHostInfoFactory)
        {
            return new RemoteHostInfoProviderDockerContainer(remoteHost, remoteHostInfoFactory);
        }
    }
}

namespace Sylas.RemoteTasks.App.DataHandlers
{
    public interface IDataHandler
    {
        Task StartAsync(params object[] parameters);
    }
}

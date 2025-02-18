using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    public class AnythingExecutor : EntityBase<int>
    {
        public string Name { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;
    }
}

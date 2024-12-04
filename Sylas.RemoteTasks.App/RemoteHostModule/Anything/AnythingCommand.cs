using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.RemoteHostModule.Anything
{
    [Table("AnythingCommands")]
    public class AnythingCommand : EntityBase<int>
    {
        public int AnythingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CommandTxt { get; set; } = string.Empty;
        public string ExecutedState { get; set; } = string.Empty;
    }
}

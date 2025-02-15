using Sylas.RemoteTasks.App.RemoteHostModule.Anything;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class CommandInfo
    {
        public string Name { get; set; } = string.Empty;
        public string CommandTxt { get; set; } = string.Empty;
        public string ExecutedState { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public AnythingCommand ToCommandEntity(int anythingId)
        {
            return new AnythingCommand
            {
                AnythingId = anythingId,
                Name = Name,
                CommandTxt = CommandTxt,
                ExecutedState = ExecutedState,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
        }
    }
}

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class DockerContainer : RemoteHostInfo
    {
        public string ContainerId { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Created { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Ports { get; set; } = string.Empty;
        /// <summary>
        /// 用容器名称表示
        /// </summary>
        public string Names { get; set; } = string.Empty;
        public override string Description { get => $"Container: {Names} [{ContainerId}] created from Image: {Image} at {Created}"; }
        /// <summary>
        /// 用容器名称表示
        /// </summary>
        public override string Name => Names;

        public override List<Tuple<string, string>> Labels
        {
            get
            {
                var result = new List<Tuple<string, string>>
                {
                    new("ContainerId", ContainerId),
                    new("Image", Image),
                    new("Command", Command),
                    new("Created", Created),
                    new("Status", Status),
                    new("Ports", Ports),
                    new("Names", Names)
                };
                return result;
            }
        }
    }

}

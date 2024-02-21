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
        public override string ShortName => ShortNameMapDictionary.TryGetValue(Name, out string? shortName) ? shortName : Name;
        public override Dictionary<string, string> ShortNameMapDictionary => new()
            {
                { "iduo.ids4", "ids4" },
                { "iduo.ids4.admin", "4admin" },
                { "iduo.ids4.api", "4api" },
                { "iduo.form.api", "fapi" },
                { "iduo.portal.api", "papi" },
                { "iduo.site.api", "sapi" },
                { "iduo.application", "app" },
                { "iduo.engine", "engine" },
                { "iduo.bpmclient", "bpm" },
            };
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

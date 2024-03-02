namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    /// <summary>
    /// 服务器信息 - 容器
    /// </summary>
    public class DockerContainer : RemoteHostInfo
    {
        /// <summary>
        /// 容器Id
        /// </summary>
        public string ContainerId { get; set; } = string.Empty;
        /// <summary>
        /// 容器的镜像
        /// </summary>
        public string Image { get; set; } = string.Empty;
        /// <summary>
        /// 容器的后台程序
        /// </summary>
        public string Command { get; set; } = string.Empty;
        /// <summary>
        /// 容器创建信息
        /// </summary>
        public string Created { get; set; } = string.Empty;
        /// <summary>
        /// 运行状态
        /// </summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>
        /// 端口信息
        /// </summary>
        public string Ports { get; set; } = string.Empty;
        /// <summary>
        /// 用容器名称表示
        /// </summary>
        public string Names { get; set; } = string.Empty;
        /// <summary>
        /// 描述
        /// </summary>
        public override string Description { get => $"Container: {Names} [{ContainerId}] created from Image: {Image} at {Created}"; }
        /// <summary>
        /// 用容器名称表示
        /// </summary>
        public override string Name => Names;
        /// <summary>
        /// 标签列表, 用于展示
        /// </summary>
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

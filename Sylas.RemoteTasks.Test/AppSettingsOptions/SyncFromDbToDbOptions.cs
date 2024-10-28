namespace Sylas.RemoteTasks.Test.AppSettingsOptions
{
    public class SyncFromDbToDbOptions
    {
        public const string Key = nameof(SyncFromDbToDbOptions);
        public string SourceConnectionString { get; set; } = string.Empty;
        public string SourceDb { get; set; } = string.Empty;
        public string SourceTable { get; set; } = string.Empty;
        public string TargetConnectionString { get; set; } = string.Empty;
        public string TargetDb { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
    }
}

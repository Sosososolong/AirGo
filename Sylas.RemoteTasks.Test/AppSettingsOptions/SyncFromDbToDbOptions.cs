using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Test.AppSettingsOptions
{
    public class SyncFromDbToDbOptions
    {
        public const string Key = nameof(SyncFromDbToDbOptions);
        public string? SourceDb { get; set; }
        public string? SourceTable { get; set; }
        public string? SourceConnectionString { get; set; }
        public string? TargetConnectionString { get; set; }
    }
}

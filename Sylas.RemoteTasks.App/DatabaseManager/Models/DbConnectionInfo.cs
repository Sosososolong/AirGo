using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.DatabaseManager.Models
{
    [Table("DbConnectionInfos")]
    public class DbConnectionInfo : EntityBase<int>
    {
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        public string Remark { get; set; } = "";
        public int OrderNo { get; set; }
    }
}

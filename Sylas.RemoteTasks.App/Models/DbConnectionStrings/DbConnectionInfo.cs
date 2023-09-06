namespace Sylas.RemoteTasks.App.Models.DbConnectionStrings
{
    public class DbConnectionInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        public string Remark { get; set; } = "";
        public int OrderNo { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
        public const string TableName = "DbConnectionInfos";
    }
}

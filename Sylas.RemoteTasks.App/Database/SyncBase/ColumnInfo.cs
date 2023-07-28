namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class ColumnInfo
    {
        public string? ColumnCode { get; set; }
        public string? ColumnName { get; set; }
        public string? ColumnType { get; set; }
        public string? ColumnCSharpType { get; set; }
        public string? ColumnLength { get; set; }
        public int IsPK { get; set; }
        public int IsNullable { get; set; }
        public int OrderNo { get; set; }
        public string? Remark { get; set; }
        public string? DefaultValue { get; set; }

    }
}

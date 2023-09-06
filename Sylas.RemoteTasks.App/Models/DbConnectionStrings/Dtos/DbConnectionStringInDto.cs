namespace Sylas.RemoteTasks.App.Models.DbConnectionStrings.Dtos
{
    public class DbConnectionStringInDto
    {
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        public string Remark { get; set; } = "";
        public int OrderNo { get; set; }
    }
}

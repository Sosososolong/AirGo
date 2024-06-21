namespace Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos
{
    public class DbConnectionInfoInDto
    {
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        public string Remark { get; set; } = "";
        public int OrderNo { get; set; }
    }

    public class DbConnectionInfoUpdateDto : DbConnectionInfoInDto
    {
        public int Id { get; set; }
    }
}

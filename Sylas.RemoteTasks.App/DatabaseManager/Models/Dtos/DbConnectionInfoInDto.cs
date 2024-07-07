using System.ComponentModel.DataAnnotations;

namespace Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos
{
    public class DbConnectionInfoInDto
    {
        public string Name { get; set; } = "";
        public string Alias { get; set; } = "";
        [Required(ErrorMessage = "连接字符串不能为空")]
        public string ConnectionString { get; set; } = "";
        public string Remark { get; set; } = "";
        public int OrderNo { get; set; }
    }

    public class DbConnectionInfoUpdateDto : DbConnectionInfoInDto
    {
        public int Id { get; set; }
    }
}

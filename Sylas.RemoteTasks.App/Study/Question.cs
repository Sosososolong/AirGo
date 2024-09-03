using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.Study
{
    [Table("Questions")]
    public class Question : EntityBase<int>
    {
        public int TypeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string? Remark { get; set; }
        public int ErrorCount { get; set; }
        public int CorrectCount { get; set; }
    }
}

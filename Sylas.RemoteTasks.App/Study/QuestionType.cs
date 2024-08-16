using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.Study
{
    [Table("QuestionTypes")]
    public class QuestionType : EntityBase<int>
    {
        public string Name { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int ParentId { get; set; }
    }
}

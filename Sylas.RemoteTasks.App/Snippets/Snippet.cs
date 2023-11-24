using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.Snippets
{
    public class Snippet : EntityBase<int>
    {
        public Snippet()
        {

        }
        public Snippet(string title, string description, string content, string tmplVariables, int typeId, DateTime createTime, DateTime updateTime)
        {
            Title = title;
            Description = description;
            Content = content;
            TmplVariables = tmplVariables;
            TypeId = typeId;
            CreateTime = createTime;
            UpdateTime = updateTime;
        }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TmplVariables { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}

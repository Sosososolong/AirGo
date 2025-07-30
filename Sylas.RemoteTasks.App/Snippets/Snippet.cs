using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.Snippets
{
    public class Snippet : EntityBase<int>
    {
        public Snippet() : base()
        {
        }
        public Snippet(string title, string description, string content, string tmplVariables, int typeId, string imageUrl, DateTime createTime, DateTime updateTime)
        {
            Title = title;
            Description = description;
            Content = content;
            TmplVariables = tmplVariables;
            TypeId = typeId;
            ImageUrl = imageUrl;
            CreateTime = createTime;
            UpdateTime = updateTime;
        }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? TmplVariables { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public string? ImageUrl { get; set; } = string.Empty;
    }
}

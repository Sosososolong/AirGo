using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.Snippets
{
    public class SnippetType : EntityBase<int>
    {
        public SnippetType()
        {
            
        }
        public SnippetType(int parentId, string name)
        {
            ParentId = parentId;
            Name = name;
        }

        public int ParentId { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}

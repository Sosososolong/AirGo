namespace Sylas.RemoteTasks.App.Utils.Template.Parser
{
    public class ParseResult
    {
        public bool Success { get; set; }
        public object? Value { get; set; }
        public ParseResult(bool success)
        {
            Success = success;
        }
        public ParseResult(bool success, object? data)
        {
            Success = success;
            Value = data;
        }
    }
}

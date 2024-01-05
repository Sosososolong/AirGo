namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    public class ParseResult
    {
        public bool Success { get; set; }
        public string[]? DataSourceKeys { get; set; }
        public object? Value { get; set; }
        public ParseResult(bool success)
        {
            Success = success;
        }
        public ParseResult(bool success, string[] sourceKey, object? result)
        {
            Success = success;
            DataSourceKeys = sourceKey;
            Value = result;
        }
    }
}

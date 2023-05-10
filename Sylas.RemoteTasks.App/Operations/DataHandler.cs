namespace Sylas.RemoteTasks.App.Operations
{
    public class DataHandler
    {
        public string Handler { get; set; }
        public List<string> Parameters { get; set; }
        public DataHandler(string handler, List<string> parameters)
        {
            Handler = handler;
            Parameters = parameters;
        }
    }
}

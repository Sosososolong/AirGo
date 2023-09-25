namespace Sylas.RemoteTasks.App.DataHandlers
{
    public class DataHandlerInfo
    {
        public string Handler { get; set; }
        public List<string> Parameters { get; set; }
        public int OrderNo { get; set; }
        public DataHandlerInfo(string handler, List<string> parameters, int orderNo)
        {
            Handler = handler;
            Parameters = parameters;
            OrderNo = orderNo;
        }
    }
}

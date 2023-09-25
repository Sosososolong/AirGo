using Newtonsoft.Json.Linq;

namespace Sylas.RemoteTasks.App.DataHandlers
{
    public class DataHandlerAnonymization : IDataHandler
    {
        public Task StartAsync(params object[] parameters)
        {
            var data = parameters[0];
            List<string> columns = parameters[1].ToString()?.Split(',')?.ToList() ?? throw new Exception("要脱敏的字段不能为空");
            if (data is IEnumerable<JToken> dataArray)
            {
                foreach (var record in dataArray)
                {
                    if (record is JObject recordObj)
                    {
                        var recordProps = recordObj.Properties();
                        foreach (var recordProp in recordProps)
                        {
                            if (columns.Any(x => x.ToLower() == recordProp.Name.ToLower()))
                            {
                                var recordPropValue = recordProp.Value.ToString();
                                if (!string.IsNullOrWhiteSpace(recordPropValue))
                                {
                                    var sublength = Convert.ToInt32(Math.Floor(recordPropValue.Length / 2.0));
                                    if (sublength == 0)
                                    {
                                        sublength = 1;
                                    }
                                    recordProp.Value = recordPropValue[..sublength] + "**";
                                }
                            }
                        }
                    }
                }
            }
            
            return Task.CompletedTask;
        }
    }
}

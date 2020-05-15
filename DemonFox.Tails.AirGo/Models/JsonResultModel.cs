using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemonFox.Tails.AirGo.Models
{
    public class JsonResultModel
    {
        public int Code { get; set; }
        public object Data { get; set; }
        public string Message { get; set; }
    }
}

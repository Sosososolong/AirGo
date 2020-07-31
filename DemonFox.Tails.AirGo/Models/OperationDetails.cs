using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemonFox.Tails.AirGo.Models
{
    public class OperationDetails
    {
        /// <summary>
        /// 配置文件中的操作, 如"OrderByDtoToEntityMaping"
        /// </summary>
        public string OperationName { get; set; }
        public string OperationDescribe { get; set; }
        public Action<OperationDetails> OperationHandler { get; set; }
        public OperationDetails(string opName, string describe, Action<OperationDetails> operationMethod)
        {
            OperationName = opName;
            OperationDescribe = describe;
            OperationHandler = operationMethod;
        }
    }
}

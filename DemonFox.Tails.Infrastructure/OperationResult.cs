using System;
using System.Collections.Generic;
using System.Text;

namespace DemonFox.Tails.Infrastructure
{
    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrMsg { get; set; }
        public List<string> Data { get; set; }
        public OperationResult(bool isSuccess, string errMsg)
        {
            IsSuccess = isSuccess;
            ErrMsg = errMsg;
        }
    }
}

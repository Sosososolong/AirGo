namespace Sylas.RemoteTasks.App.Infrastructure
{
    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrMsg { get; set; }
        public List<string>? Data { get; set; }
        /// <summary>
        /// 初始化是否成功和错误信息
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <param name="errMsg"></param>
        public OperationResult(bool isSuccess, string errMsg)
        {
            IsSuccess = isSuccess;
            ErrMsg = errMsg;
        }
    }
}

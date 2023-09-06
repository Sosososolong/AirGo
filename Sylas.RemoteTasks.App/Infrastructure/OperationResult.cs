namespace Sylas.RemoteTasks.App.Infrastructure
{
    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrMsg { get; set; } = string.Empty;
        public IEnumerable<string>? Data { get; set; }
        /// <summary>
        /// 是否成功
        /// </summary>
        /// <param name="isSuccess"></param>
        public OperationResult(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
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
        /// <summary>
        /// 操作成功返回信息
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <param name="data"></param>
        public OperationResult(bool isSuccess, IEnumerable<string> data)
        {
            IsSuccess = isSuccess;
            Data = data;
        }
    }
}

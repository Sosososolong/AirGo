using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.Dto
{
    /// <summary>
    /// 存储操作结果的相关信息
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrMsg { get; set; } = string.Empty;
        /// <summary>
        /// 查询的数据
        /// </summary>
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

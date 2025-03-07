using System.Collections.Generic;

namespace Sylas.RemoteTasks.Common.Dtos
{
    /// <summary>
    /// 存储操作结果的相关信息
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Succeed { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// 查询的数据
        /// </summary>
        public IEnumerable<string>? Data { get; set; }
        /// <summary>
        /// 是否成功
        /// </summary>
        /// <param name="succeed"></param>
        public OperationResult(bool succeed)
        {
            Succeed = succeed;
        }
        /// <summary>
        /// 初始化是否成功和错误信息
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <param name="errMsg"></param>
        public OperationResult(bool isSuccess, string errMsg)
        {
            Succeed = isSuccess;
            Message = errMsg;
        }
        /// <summary>
        /// 操作成功返回信息
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <param name="data"></param>
        public OperationResult(bool isSuccess, IEnumerable<string> data)
        {
            Succeed = isSuccess;
            Data = data;
        }
    }
}

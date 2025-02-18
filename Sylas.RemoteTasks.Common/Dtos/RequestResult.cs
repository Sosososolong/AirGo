namespace Sylas.RemoteTasks.Common.Dtos
{
    /// <summary>
    /// 接口请求响应结果
    /// </summary>
    public class RequestResult<T>
    {
        /// <summary>
        /// 默认值初始化属性
        /// </summary>
        public RequestResult()
        {
            Code = 1;
            ErrMsg = string.Empty;
            Data = default;
        }
        /// <summary>
        /// 成功返回数据的实例
        /// </summary>
        /// <param name="data"></param>
        public RequestResult(T data)
        {
            Code = 1;
            ErrMsg = string.Empty;
            Data = data;
        }
        /// <summary>
        /// 指定所有属性值初始化实例
        /// </summary>
        /// <param name="code"></param>
        /// <param name="data"></param>
        /// <param name="errMsg"></param>
        private RequestResult(int code, T? data, string errMsg)
        {
            Code = code;
            ErrMsg = errMsg;
            Data = data;
        }
        /// <summary>
        /// 返回通用错误结果
        /// </summary>
        /// <param name="errMsg"></param>
        /// <returns></returns>
        public static RequestResult<T> Error(string errMsg) => new(0, default, errMsg);
        /// <summary>
        /// 返回成功结果
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static RequestResult<T> Success(T t) => new(1, t, "");
        /// <summary>
        /// 状态码
        /// </summary>
        public int Code { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrMsg { get; set; }
        /// <summary>
        /// 接口响应的数据
        /// </summary>
        public T? Data { get; set; }
    }
}

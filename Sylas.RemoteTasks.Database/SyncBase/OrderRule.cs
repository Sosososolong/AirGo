namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 排序规则
    /// </summary>
    public class OrderRule
    {
        /// <summary>
        /// 使用默认排序字段和排序方式
        /// </summary>
        public OrderRule()
        {
            
        }
        /// <summary>
        /// 使用指定排序字段和排序方式
        /// </summary>
        /// <param name="field"></param>
        /// <param name="isAsc"></param>
        public OrderRule(string field, bool isAsc)
        {
            OrderField = field;
            IsAsc = isAsc;
        }
        /// <summary>
        /// 排序字段
        /// </summary>
        public string OrderField { get; set; } = "updatetime";
        /// <summary>
        /// 是否升序
        /// </summary>
        public bool IsAsc { get; set; } = false;
    }
}

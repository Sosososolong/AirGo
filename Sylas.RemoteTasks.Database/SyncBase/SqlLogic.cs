namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// SQL条件语句的逻辑关系
    /// </summary>
    public enum SqlLogic
    {
        /// <summary>
        /// 且
        /// </summary>
        And,
        /// <summary>
        /// 或
        /// </summary>
        Or,
        /// <summary>
        /// 当个条件时, 不需要逻辑关系
        /// </summary>
        None
    }
}

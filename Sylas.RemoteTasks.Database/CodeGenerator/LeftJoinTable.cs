using Sylas.RemoteTasks.Database.SyncBase;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Database.CodeGenerator
{
    /// <summary>
    /// 左联联表信息
    /// </summary>
    public class LeftJoinTable : CurdTableInfo
    {
        /// <summary>
        /// 无参构造函数
        /// </summary>
        public LeftJoinTable()
        {
            
        }
        /// <summary>
        /// 构造函数初始化所有属性
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="alias"></param>
        /// <param name="selfRelatedField"></param>
        /// <param name="otherRelatedField"></param>
        /// <param name="columnInfos"></param>
        /// <param name="showingColumns"></param>
        public LeftJoinTable(string tableName, string alias, string selfRelatedField = "", string otherRelatedField = "", List<ColumnInfo>? columnInfos = null, List<ShowingColumn>? showingColumns = null) : base(tableName, alias, columnInfos, showingColumns)
        {
            SelfRelatedField = selfRelatedField;
            OtherRelatedField = otherRelatedField;
        }
        /// <summary>
        /// 关联字段 - 左表字段
        /// </summary>
        public string SelfRelatedField { get; set; } = string.Empty;
        /// <summary>
        /// 关联字段 - 右表字段
        /// </summary>
        public string OtherRelatedField { get; set; } = string.Empty;
    }
}

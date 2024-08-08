using Sylas.RemoteTasks.Database.SyncBase;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Database.CodeGenerator
{
    /// <summary>
    /// 需要增删改查的表信息
    /// </summary>
    public class CurdTableInfo
    {
        /// <summary>
        /// 无参构造函数
        /// </summary>
        public CurdTableInfo()
        {
            
        }
        /// <summary>
        /// 构造函数初始化所有属性
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="alias"></param>
        /// <param name="columnInfos"></param>
        /// <param name="showingColumns"></param>
        public CurdTableInfo(string tableName, string alias, List<ColumnInfo>? columnInfos = null, List<ShowingColumn>? showingColumns = null)
        {
            Name = tableName;
            Alias = alias;
            Columns = columnInfos ?? [];
            ShowingColumns = showingColumns ?? [];
        }
        /// <summary>
        /// 左联表表名
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 左联表别名
        /// </summary>
        public string Alias { get; set; } = string.Empty;
        /// <summary>
        /// 关联表的所有字段
        /// </summary>
        public List<ColumnInfo> Columns { get; set; } = [];
        /// <summary>
        /// 关联表需要显示的字段
        /// </summary>
        public List<ShowingColumn> ShowingColumns { get; set; } = [];
    }
}

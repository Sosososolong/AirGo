using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// Select语句构建器
    /// </summary>
    public class QuerySqlBuilder
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="databaseType"></param>
        public QuerySqlBuilder(DatabaseType databaseType)
        {
            _databaseType = databaseType;
            _varFlag = _databaseType switch
            {
                DatabaseType.Oracle => ":",
                DatabaseType.Dm => ":",
                _ => "@"
            };
        }
        /// <summary>
        /// 切换数据库类型
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        public static QuerySqlBuilder UseDatabase(DatabaseType databaseType) => new(databaseType);
        readonly DatabaseType _databaseType;
        readonly string _varFlag;
        /// <summary>
        /// 要查询的所有表, 第一个是主表, 其他为联查表
        /// </summary>
        public List<QueryTable> QueryTables { get; set; } = [];

        private readonly Dictionary<string, object?> _parameters = [];
        /// <summary>
        /// 条件组 - 用来描述一个查询语句的所有条件
        /// </summary>
        public FilterGroup? FilterGroup { get; set; }
        string _orderBy = string.Empty;
        int _pageIndex = 0;
        int _pageSize = 0;
        /// <summary>
        /// 构建Select语句部分
        /// </summary>
        /// <param name="queryTable"></param>
        /// <returns></returns>
        public QuerySqlBuilder Select(QueryTable queryTable)
        {
            QueryTables.Add(queryTable);
            return this;
        }
        /// <summary>
        /// 构建Select语句部分
        /// </summary>
        /// <param name="table"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        public QuerySqlBuilder Select(string table, string alias = "")
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                alias = $"alias_{table}";
            }
            var queryTable = new QueryTable
            {
                TableName = table,
                Alias = alias,
                SelectColumns = null,
                OnConditions = null
            };
            QueryTables.Add(queryTable);
            return this;
        }
        /// <summary>
        /// 构建Select语句部分
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columns"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        public QuerySqlBuilder Select(string table, string[] columns, string alias = "")
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                alias = $"alias_{table}";
            }
            var queryTable = new QueryTable
            {
                TableName = table,
                Alias = alias,
                SelectColumns = columns,
                OnConditions = null
            };
            QueryTables.Add(queryTable);
            return this;
        }
        /// <summary>
        /// 构建Left Join语句部分
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filterGroup"></param>
        /// <returns></returns>
        public QuerySqlBuilder LeftJoin(string table, FilterGroup filterGroup)
        {
            var queryTable = new QueryTable
            {
                TableName = table,
                SelectColumns = null,
                OnConditions = filterGroup
            };
            QueryTables.Add(queryTable);
            return this;
        }
        /// <summary>
        /// 构建Left Join语句部分
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columns"></param>
        /// <param name="filterGroup"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public QuerySqlBuilder LeftJoin(string table, string[] columns, FilterGroup filterGroup, string tableAlias = "")
        {
            if (string.IsNullOrWhiteSpace(tableAlias))
            {
                tableAlias = $"alias_{table}";
            }
            var queryTable = new QueryTable
            {
                TableName = table,
                Alias = tableAlias,
                SelectColumns = columns,
                OnConditions = filterGroup
            };
            QueryTables.Add(queryTable);
            return this;
        }
        /// <summary>
        /// 构建Left Join语句部分
        /// </summary>
        /// <param name="leftJoinTables"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public QuerySqlBuilder LeftJoins(IEnumerable<QueryTable> leftJoinTables)
        {
            if (leftJoinTables is null || !leftJoinTables.Any())
            {
                return this;
            }
            var badLeftJoin = leftJoinTables.FirstOrDefault(x => x.OnConditions is null);
            if (badLeftJoin is not null)
            {
                throw new Exception($"联查表{badLeftJoin.TableName}未设置关联条件");
            }
            foreach (var leftJoinTable in leftJoinTables)
            {
                if (string.IsNullOrWhiteSpace(leftJoinTable.TableName))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(leftJoinTable.Alias))
                {
                    leftJoinTable.Alias = $"alias_{leftJoinTable.TableName}";
                }
                QueryTables.Add(leftJoinTable);
            }
            return this;
        }
        /// <summary>
        /// 构建where语句
        /// </summary>
        /// <param name="filterGroup"></param>
        /// <returns></returns>
        public QuerySqlBuilder Where(FilterGroup filterGroup)
        {
            FilterGroup = filterGroup;
            return this;
        }

        SqlGroupInfo _groupInfo = new();
        /// <summary>
        /// 构建Group语句
        /// </summary>
        /// <param name="groupInfo"></param>
        /// <returns></returns>
        public QuerySqlBuilder Group(SqlGroupInfo groupInfo)
        {
            _groupInfo = groupInfo;
            return this;
        }
        /// <summary>
        /// 构建OrderBy语句
        /// </summary>
        /// <param name="orderByFields"></param>
        /// <returns></returns>
        public QuerySqlBuilder OrderBy(params OrderField[] orderByFields)
        {
            if (orderByFields is null || orderByFields.Length == 0)
            {
                return this;
            }
            foreach (var orderByField in orderByFields)
            {
                if (string.IsNullOrWhiteSpace(orderByField.FieldName))
                {
                    continue;
                }

                string orderType = orderByField.IsAsc ? "ASC" : "DESC";
                if (string.IsNullOrWhiteSpace(_orderBy))
                {
                    _orderBy = $"ORDER BY {orderByField.FieldName} {orderType}";
                }
                else
                {
                    _orderBy += $", {orderByField.FieldName} {orderType}";
                }
            }
            return this;
        }
        /// <summary>
        /// 接收分页参数
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public QuerySqlBuilder Page(SqlPage page)
        {
            int pageIndex = 1;
            int pageSize = 10;

            if (page is not null)
            {
                if (page.PageIndex > 0)
                {
                    pageIndex = page.PageIndex;
                }
                if (page.PageSize > 0)
                {
                    pageSize = page.PageSize;
                }
            }

            _pageIndex = pageIndex;
            _pageSize = pageSize;
            return this;
        }
        static string GetSelectColumnStatement(QueryTable table)
        {
            bool mainTable = table.OnConditions is null || !table.OnConditions.FilterItems.Any();
            if (table.SelectColumns is null)
            {
                string columnsStatement = $"{table.Alias}.*";
                return mainTable ? columnsStatement : $",{columnsStatement}";
            }
            else if (table.SelectColumns.Length > 0)
            {
                string columnsStatement = string.Join(',', table.SelectColumns.Select(x => $"{table.Alias}.{x}"));
                return mainTable ? columnsStatement : $",{columnsStatement}";
            }
            else
            {
                // 等于0时, 不添加任何字段
                return string.Empty;
            }
        }
        /// <summary>
        /// 构建完整的查询语句
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public SqlInfo Build()
        {
            if (QueryTables.Count == 0)
            {
                throw new Exception("未设置数据表");
            }

            StringBuilder querySqlBuilder = new();
            StringBuilder selectFieldsBuilder = new();

            if (QueryTables.Count > 0)
            {
                // 主表
                var mainTable = QueryTables[0];
                querySqlBuilder.Append($"FROM {mainTable.TableName} AS {mainTable.Alias} ");
                string columnsStatement = GetSelectColumnStatement(mainTable);
                selectFieldsBuilder.Append(columnsStatement);

                // 处理联查表
                for (int i = 1; i < QueryTables.Count; i++)
                {
                    var joinTable = QueryTables[i];
                    if (joinTable is null || joinTable.OnConditions is null)
                    {
                        throw new Exception($"缺少联查表{joinTable?.TableName}信息");
                    }
                    querySqlBuilder.Append($"LEFT JOIN {joinTable.TableName} AS {joinTable.Alias} ON ");
                    querySqlBuilder.Append(joinTable.OnConditions.ToString());
                    querySqlBuilder.Append(' ');

                    columnsStatement = GetSelectColumnStatement(joinTable);
                    selectFieldsBuilder.Append(columnsStatement);
                }
            }

            // 处理 WHERE 条件
            if (FilterGroup != null && FilterGroup.FilterItems is not null && FilterGroup.FilterItems.Any())
            {
                var sqlInfo = FilterGroup.BuildConditions(_databaseType);
                if (!string.IsNullOrWhiteSpace(sqlInfo.Sql))
                {
                    querySqlBuilder.Append($"WHERE {sqlInfo.Sql}");
                }
                foreach (var item in sqlInfo.Parameters as Dictionary<string, object>)
                {
                    _parameters.Add(item.Key, item.Value);
                }
            }

            // GroupBy
            string groupBy = " ";
            if (_groupInfo is not null && !string.IsNullOrWhiteSpace(_groupInfo.FieldName))
            {
                groupBy = $" GROUP BY {_groupInfo.FieldName} ";
                if (_groupInfo.Having is not null)
                {
                    var havingSqlInfo = _groupInfo.Having.BuildConditions(_databaseType);
                    groupBy += $"HAVING {havingSqlInfo.Sql} ";
                    foreach (var item in havingSqlInfo.Parameters as Dictionary<string, object>)
                    {
                        if (_parameters.ContainsKey(item.Key))
                        {
                            var last = _parameters.Keys.Where(x => x.StartsWith(item.Key)).OrderBy(x => x).Last();
                            var lastIndexStr = last.Replace(item.Key, string.Empty);
                            if (string.IsNullOrWhiteSpace(lastIndexStr))
                            {
                                lastIndexStr = "0";
                            }
                            int lastIndex = int.Parse(lastIndexStr);
                            string newItemKey = $"{item.Key}{lastIndex + 1}";

                            groupBy = groupBy.Replace($"{_varFlag}{item.Key}", $"{_varFlag}{newItemKey}");
                            _parameters.Add(newItemKey, item.Value);
                        }
                        else
                        {
                            _parameters.Add(item.Key, item.Value);
                        }
                    }
                }
            }
            string finallySelectColumns = selectFieldsBuilder.ToString();
            if (string.IsNullOrWhiteSpace(finallySelectColumns))
            {
                finallySelectColumns = "*";
            }
            string sql = $"SELECT {finallySelectColumns} {querySqlBuilder}{groupBy}{_orderBy}";

            #region 分页
            if (_pageIndex > 0 && _pageSize > 0)
            {
                sql = _databaseType switch
                {
                    _ when _databaseType == DatabaseType.Oracle || _databaseType == DatabaseType.Dm => $@"SELECT T.*, ROWNUM NO FROM ({sql}) t WHERE ROWNUM>({_pageIndex}-1)*{_pageSize} AND ROWNUM<=({_pageIndex})*{_pageSize}",

                    DatabaseType.MySql => $@"{sql} LIMIT {(_pageIndex - 1) * _pageSize},{_pageSize}",

                    DatabaseType.Pg => $@"{sql} limit {_pageSize} offset {(_pageIndex - 1) * _pageSize}",

                    DatabaseType.SqlServer => $"{sql} OFFSET ({_pageIndex}-1)*{_pageSize} ROWS FETCH NEXT {_pageSize} ROW ONLY",

                    DatabaseType.Sqlite => $@"{sql} LIMIT {_pageSize} OFFSET {(_pageIndex - 1) * _pageSize}",

                    _ => $@"{sql} OFFSET ( {_pageIndex} - 1) * {_pageSize} ROWS FETCH NEXT {_pageSize} ROW ONLY",
                };
            }
            #endregion

            return new SqlInfo(sql, _parameters);
        }
    }
}

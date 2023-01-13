//using MySqlConnector;
//using Microsoft.Data.SqlClient;
using Dapper;
using MySql.Data.MySqlClient;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.RegexExp;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    #region TODO
    // ...实现从任意数据库 生成创建MySQL表SQL(字段类型)
    // 1. 实现任意数据库到任意数据库的新增同步
    // 2. 实现对比数据进行新增,更新或删除(已经实现GetSyncData)
    // 3. 尝试重构
    #endregion

    public static partial class DatabaseInfo
    {
        #region 数据库连接对象
        public static IDbConnection GetDbConnection(string connectionString)
        {
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(connectionString);
            return dbType switch
            {
                DatabaseType.MySql => new MySqlConnection(connectionString),
                DatabaseType.Oracle => new OracleConnection(connectionString),
                DatabaseType.SqlServer => new SqlConnection(connectionString),
                _ => throw new Exception($"不支持的数据库连接字符串: {connectionString}"),
            };
        }
        public static IDbConnection GetOracleConnection(string host, string port, string instanceName, string username, string password) => new OracleConnection($"Data Source={host}:{port}/{instanceName};User ID={username};Password={password};PERSIST SECURITY INFO=True;Pooling = True;Max Pool Size = 100;Min Pool Size = 1;");
        public static IDbConnection GetMySqlConnection(string host, string port, string db, string username, string password) => new MySqlConnection($"Server={host};Port={port};Stmt=;Database={db};Uid={username};Pwd={password};Allow User Variables=true;");
        public static IDbConnection GetSqlServerConnection(string host, string port, string db, string username, string password) => new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host}");
        #endregion

        public static string GetAllTables(string dbName)
        {
            return $"select * from information_schema.`TABLES` WHERE table_schema='{dbName}'";
        }
        public static async Task<bool> TableExist(this IDbConnection conn, string table)
        {
            var databaseType = GetDbType(conn.ConnectionString);
            var checkSql = string.Empty;
            switch (databaseType)
            {
                case DatabaseType.Oracle:
                    checkSql = $"select count(*) from all_tables where owner=upper('{conn.Database}') and table_name=upper('{table}')";
                    break;
                case DatabaseType.MySql:
                    checkSql = $"select count(*) from information_schema.tables where table_name='{table}' and table_schema=(select database())";
                    break;
                case DatabaseType.SqlServer:
                    checkSql = $"select count(*) from sysobjects where id = object_id('{table}') and OBJECTPROPERTY(id, N'IsUserTable') = 1";
                    break;
                default:
                    break;
            }
            var tableCount = await conn.ExecuteScalarAsync<int>(checkSql);
            return tableCount > 0;
        }
        public static string GetTableFullName(dynamic tableDynamic)
        {
            return $"{tableDynamic.TABLESPACE_NAME}.{tableDynamic.TABLE_NAME}";
        }
        private static void ValideParameter(string parameter)
        {
            if (parameter.Contains('-') || parameter.Contains('\''))
            {
                throw new Exception($"参数{parameter}不合法");
            }
        }

        public static async Task<PagedData> GetPagedData(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, IDbConnection dbConn, DataFilter filters)
        {
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(dbConn.ConnectionString);
            string orderClause = string.Empty;
            if (!string.IsNullOrWhiteSpace(orderField))
            {
                orderField = orderField.Replace("-", string.Empty).Replace("'", string.Empty);
                ValideParameter(orderField);
                orderClause = $" order by {orderField} {(isAsc ? "asc" : "desc")}";
            }
            string parameterFlag = dbType == DatabaseType.Oracle ? ":" : "@";

            #region 处理过滤条件
            string condition = string.Empty;
            Dictionary<string, object> parameters = new();
            if (filters != null)
            {
                if (filters.FilterItems != null && filters.FilterItems.Count > 0)
                {
                    var filterItems = filters.FilterItems;
                    foreach (var filterField in filterItems)
                    {
                        if (filterField.FieldName is null || filterField.CompareType is null || filterField.Value is null || string.IsNullOrWhiteSpace(filterField.Value.ToString()))
                        {
                            continue;
                        }
                        var key = filterField.FieldName;
                        if (!parameters.ContainsKey(key))
                        {
                            var compareType = filterField.CompareType;
                            var compareTypes = new List<string> { ">", "<", "=", ">=", "<=", "!=", "include" };
                            if (!compareTypes.Contains(compareType))
                            {
                                throw new ArgumentException("过滤条件比较类型不正确");
                            }
                            var val = filterField.Value;
                            ValideParameter(key);
                            if (compareType == "include")
                            {
                                condition += $" and {key} like CONCAT(CONCAT('%', {parameterFlag}{key}), '%')";
                            }
                            else
                            {
                                condition += $" and {key}{compareType}{parameterFlag}{key}";
                            }
                            parameters.Add(key, val);
                        }
                    }
                }
                if (filters.Keywords != null && filters.Keywords.Fields != null && filters.Keywords.Fields.Length > 0 && !string.IsNullOrWhiteSpace(filters.Keywords.Value))
                {
                    var keyWordsCondition = string.Empty;
                    foreach (var keyWordsField in filters.Keywords.Fields)
                    {
                        string varName = $"keyWords{keyWordsField}";
                        if (!parameters.ContainsKey(varName))
                        {
                            keyWordsCondition += $" or {keyWordsField} like CONCAT(CONCAT('%', {parameterFlag}{varName}), '%')";
                            parameters.Add(varName, filters.Keywords.Value);
                        }
                    }
                    if (!string.IsNullOrEmpty(keyWordsCondition))
                    {
                        condition += $" and ({keyWordsCondition[4..]})";
                    }
                }
            }
            #endregion

            string baseSqlTxt = $"select * from {table} where 1=1 {condition}";
            string allCountSqlTxt = $"select count(*) from {table} where 1=1 {condition}";

            string sql = string.Empty;
            #region 不同数据库对应的SQL语句
            switch (dbType)
            {
                case DatabaseType.Oracle:
                    sql = $@"select * from 
	(
	select t.*,rownum no from 
		(
		{baseSqlTxt} {orderClause}
		) t 
	)
where no>({pageIndex}-1)*{pageSize} and no<=({pageIndex})*{pageSize}";
                    break;
                case DatabaseType.MySql:
                    sql = $@"{baseSqlTxt} {orderClause} limit {(pageIndex - 1) * pageSize},{pageSize}";
                    break;
                case DatabaseType.SqlServer:
                    sql = $"{baseSqlTxt} {(string.IsNullOrEmpty(orderClause) ? "order by id desc" : orderClause)} OFFSET ({pageIndex}-1)*{pageSize} ROWS FETCH NEXT {pageSize} ROW ONLY";
                    break;
                default:
                    sql = $@"{baseSqlTxt} {(string.IsNullOrEmpty(orderClause) ? "order by id desc" : orderClause)} OFFSET ({pageIndex}-1)*{pageSize} ROWS FETCH NEXT {pageSize} ROW ONLY";
                    break;
            }
            #endregion

            var data = await dbConn.QueryAsync(sql, parameters);
            var dataReader = await dbConn.ExecuteReaderAsync(sql, parameters);
            var allCount = await dbConn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);
            return new PagedData { Data = data, DataReader = dataReader, Count = allCount };
        }

        // 获取insert语句的字段部分 Name,Age
        public static string GenerateColumnsStatement(IEnumerable<ColumnInfo> colInfos)
        {
            var columnBuilder = new StringBuilder();
            foreach (var colInfo in colInfos)
            {
                var columnName = colInfo.ColumnName;
                columnBuilder.Append($"{columnName},");
            }
            return columnBuilder.ToString().TrimEnd(',');
        }
        public static object GetColumnValue(object value, string type)
        {
            if (value.GetType().Name == "DBNull")
            {
                if (!string.IsNullOrWhiteSpace(type))
                {
                    var lowerType = type.ToLower();
                    if (lowerType.Contains("varchar") || lowerType.Contains("clob"))
                    {
                        return string.Empty;
                    }
                    if (lowerType.Contains("time"))
                    {
                        return DateTime.Now;
                    }
                    if (lowerType.Contains("int"))
                    {
                        return 0;
                    }
                    if (lowerType.Contains("byte") || lowerType.Contains("varbinary") || lowerType.Contains("blob"))
                    {
                        return Array.Empty<byte>();
                    }
                    throw new Exception($"未处理的DBNull参数: {type}");
                }
            }
            return value;
        }

        // 获取创建表的字段声明部分
        public static string GenerateColumnsAssignment(IEnumerable<ColumnInfo> colInfos, DatabaseType dbType)
        {
            // 处理一条数据
            StringBuilder columnBuilder = new StringBuilder();
            foreach (var colInfo in colInfos)
            {
                // Age
                var columnName = colInfo.ColumnName;
                var timeTypeDefine = colInfo.ColumnType ?? string.Empty;
                switch (dbType)
                {
                    case DatabaseType.MySql:
                        columnName = $"`{columnName}`";
                        timeTypeDefine = $"datetime(6){Environment.NewLine}";
                        break;
                    case DatabaseType.Oracle:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"TIMESTAMP(6) WITH TIME ZONE{Environment.NewLine}";
                        break;
                    case DatabaseType.SqlServer:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"datetime2(7){Environment.NewLine}";
                        break;
                    default:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"datetime(6){Environment.NewLine}";
                        break;
                }

                if (string.IsNullOrWhiteSpace(colInfo.ColumnType))
                {
                    throw new Exception($"字段{colInfo.ColumnName}的类型为空");
                }

                var columnTypeAndLength = colInfo.ColumnType switch
                {
                    // 长整型字段
                    var type when RegexConst.ColumnTypeLong().IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => $"long{Environment.NewLine}",
                        _ => $"bigint{Environment.NewLine}"
                    },
                    // 整型字段
                    var type when RegexConst.ColumnTypeInt().IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => $"number",
                        _ => $"int{Environment.NewLine}"
                    },
                    var type when type.ToLower().Contains("datetime") => timeTypeDefine,
                    var type when type.ToLower().Contains("timestamp") => timeTypeDefine,
                    // 大文本字段
                    var type when type.ToLower().Contains("clob") => dbType switch
                    {
                        DatabaseType.Oracle => $"blob{Environment.NewLine}",
                        _ => $"text{Environment.NewLine}"
                    },
                    // 二进制字段
                    var type when RegexConst.ColumnTypeBlob().IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => "blob",
                        _ => $"mediumblob{Environment.NewLine}"
                    },
                    var type when type.ToLower().Contains("varchar") && Convert.ToInt32(colInfo.ColumnLength) > 0 => $"varchar({colInfo.ColumnLength}){Environment.NewLine}",
                    var type when type.ToLower().Contains("string") && Convert.ToInt32(colInfo.ColumnLength) > 2000000000 => $"blob{Environment.NewLine}",
                    var type when type.ToLower().Contains("string") && Convert.ToInt32(colInfo.ColumnLength) < 2000 => $"nvarchar({(colInfo.ColumnLength == "-1" ? string.Empty : colInfo.ColumnLength)}){Environment.NewLine}",
                    // 普通字符串字段
                    _ => dbType switch
                    {
                        DatabaseType.Oracle => $"nvarchar2({colInfo.ColumnLength})",
                        DatabaseType.MySql => $"nvarchar({colInfo.ColumnLength}){Environment.NewLine}",
                        _ => $"varchar({colInfo.ColumnLength}){Environment.NewLine}"
                    }
                };
                columnBuilder.Append($"{columnName} {columnTypeAndLength},");
            }

            return columnBuilder.ToString().TrimEnd(',');
        }

        // 根据一条DataRow对象 生成对应的insert语句的 字段部分和值(参数)部分: Name,Age  @Name1,@Age1
        public static string GenerateRecordValuesStatement(DataRow dataItem, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object> parameters, string dbVarFlag, int random = 0)
        {
            // 处理一条数据
            var valueBuilder = new StringBuilder();
            foreach (var colInfo in colInfos)
            {
                var columnName = colInfo.ColumnName;
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new Exception($"包含空字段");
                }
                var columnValue = dataItem[columnName];
                var columnType = colInfo.ColumnType ?? string.Empty;
                var parameterName = $"{columnName}{random}";

                valueBuilder.Append($"{dbVarFlag}{parameterName},");

                parameters.Add(parameterName, GetColumnValue(columnValue, columnType));
            }

            return valueBuilder.ToString().TrimEnd(',');
        }

        // 生成一条insert语句 - mysql, 不同于Oracle,这里不需要参数: string targetTable, string columnsStatement,
        public static string GenerateInsertSql(DataRow dataItem, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object> parameters, string dbVarFlag, int random = 0)
        {
            // "@Name1, :Age1"
            string valuesStatement = GenerateRecordValuesStatement(dataItem, colInfos, parameters, dbVarFlag, random);
            // ("@Name1, :Age1"),
            string currentDataRowInsertStatement = $"({valuesStatement}),{Environment.NewLine}";
            return currentDataRowInsertStatement;
        }
        /// <summary>生成创建表的SQL语句</summary>
        public static string GenerateCreateTableSql(string tableName, IEnumerable<ColumnInfo> colInfos, DatabaseType dbType)
        {
            var columnsAssignment = GenerateColumnsAssignment(colInfos, dbType);
            // TODO: 主键primaryKey 作为公共属性
            var columnPrimaryKey = colInfos.FirstOrDefault(c => c.IsPK == 1);
            var primaryKeyAssignment = columnPrimaryKey is null ? string.Empty : $", PRIMARY KEY (`{columnPrimaryKey.ColumnName}`) USING BTREE";
            string createSql = $@"CREATE TABLE `{tableName}` (
		  {columnsAssignment}
		  {primaryKeyAssignment}
		) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;";
            return createSql;
        }

        /// <summary>获取数据源中一个表的create语句和数据的insert语句</summary>
        public static async Task<TableSqlsInfo> GetTableSqlsInfoAsync(dynamic table, IDbConnection conn, IDbTransaction trans, IDbConnection targetConn, DataFilter filter)
        {
            // IDUO.IDS4.DEPARTMENT
            string targetTable = GetTableFullName(table);
            if (targetTable.Split('.').Last().StartsWith("_"))
            {
                return new TableSqlsInfo
                {
                    TableName = targetTable
                };
            }
            return await GetTableSqlsInfoAsync(targetTable, conn, trans, targetConn, filter);
        }
        public static DatabaseType GetDbType(string connectionString)
        {
            var lowerConnStr = connectionString.ToLower();
            return lowerConnStr switch
            {
                var connStr when connStr.Contains("initial catalog") => DatabaseType.SqlServer,
                var connStr when connStr.Contains("server") => DatabaseType.MySql,
                _ => DatabaseType.Oracle
            };
        }

        /// <summary>获取数据源中一个表的数据的insert语句</summary>
        public static async Task<TableSqlsInfo> GetTableSqlsInfoAsync(string sourceTable, IDbConnection sourceConn, IDbTransaction trans, IDbConnection targetConn, DataFilter filter)
        {
            var tableName = sourceTable.Split('.').Last();
            // 1. 获取源表的所有字段信息
            #region 获取源表的所有字段信息
            IEnumerable<ColumnInfo> colInfos = await GetTableColsInfoAsync(sourceConn, tableName);
            var primaryKey = colInfos.FirstOrDefault(x => x.IsPK == 1)?.ColumnName;
            #endregion

            // 2. 获取源表所有数据
            #region 获取源表所有数据
            DataTable sourceDataTable = new(tableName);
            try
            {
                var srouceTableDataReader = (await GetPagedData(sourceTable, 1, 3000, primaryKey, true, sourceConn, filter)).DataReader;
                if (srouceTableDataReader is null)
                {
                    throw new Exception($"表{sourceTable}的数据读取器为空");
                }
                sourceDataTable.Load(srouceTableDataReader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询数据出错targetTableDataSql: {sourceTable}");
                Console.WriteLine(ex.ToString());
            }
            #endregion

            // 3. 获取目标表的数据, 没有则创建
            #region 获取目标表的数据
            var targetDataTable = new DataTable();
            string createSql = string.Empty;
            try
            {
                targetDataTable = await QueryAsync(targetConn, $"select * from {tableName}", new Dictionary<string, object>(), trans);
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("doesn't exist"))
                {
                    // 表不存在创建表
                    // TODO: 数据库类型 作为公共属性
                    createSql = GenerateCreateTableSql(tableName, colInfos, GetDbType(targetConn.ConnectionString));
                    //await targetConn.ExecuteAsync(createSql);
                    //targetDataTable = await QueryAsync(targetConn, $"select * from {tableName}", new Dictionary<string, object>(), trans);
                }
            }
            #endregion

            // 4. 对比原表和目标表数据, 目标表没有的数据就创建对应的insert语句
            #region 对比原表和目标表数据, 目标表没有的数据就创建对应的insert语句
            var insertSqlBuilder = new StringBuilder();
            // insert语句参数
            var parameters = new Dictionary<string, object>();
            // 参数名后缀(第一条数据的Name字段, 参数名为Name1, 第二条为Name2, ...)
            var random = 1;

            if (sourceDataTable.Rows.Count == 0)
            {
                return new TableSqlsInfo
                {
                    TableName = sourceTable
                };
            }

            // columnsStatement =           "Name, Age"
            string columnsStatement = GenerateColumnsStatement(colInfos);

            // TODO: 数据库类型 作为公共属性
            var dbVarFlag = GetDbType(targetConn.ConnectionString) == DatabaseType.Oracle ? ":" : "@";
            // 为数据源中的每一条数据生成对应的insert语句的部分
            foreach (DataRow dataItem in sourceDataTable.Rows)
            {
                // 重复的数据不做处理
                if (targetDataTable.Rows.Count > 0 && AlreadyExistInTarget(targetDataTable.Rows, dataItem))
                {
                    continue;
                }

                // (@Name1, @Age1),
                string currentRecordSql = GenerateInsertSql(dataItem, colInfos, parameters, dbVarFlag, random);
                insertSqlBuilder.Append($"{currentRecordSql}");
                random++;
            }
            // 如果表中已经存在所有数据, 那么insertSqlBuilder为空的
            if (insertSqlBuilder.Length == 0)
            {
                Console.WriteLine($"{sourceTable}已经拥有所有记录");
                return new TableSqlsInfo
                {
                    TableName = sourceTable,
                };
            }
            // 最终的insert语句
            var targetTableAllDataInsertSql = $"insert into {sourceTable}({columnsStatement}) values{Environment.NewLine}{insertSqlBuilder.ToString().TrimEnd(',', '\r', '\n')};";
            #endregion
            return new TableSqlsInfo
            {
                TableName = sourceTable,

                BatchInsertSqlInfo = new BatchInsertSqlInfo()
                {
                    CreateTableSql = createSql,
                    // mysql 本次会话取消外键约束检查
                    BatchInsertSql = $"SET foreign_key_checks=0;{Environment.NewLine}{targetTableAllDataInsertSql}",
                    Parameters = parameters
                }
            };
        }
        public static string GetDatabaseName(string connectionString)
        {
            var sqlServerDb = RegexConst.SqlServerDbName().Match(connectionString).Value;
            if (!string.IsNullOrEmpty(sqlServerDb))
            {
                return sqlServerDb;
            }

            var oracleDb = RegexConst.OracleDbName().Match(connectionString).Value;
            if (!string.IsNullOrEmpty(oracleDb))
            {
                return oracleDb;
            }

            var mysqlDb = RegexConst.MySqlDbName().Match(connectionString).Value;
            return mysqlDb;
        }
        /// <summary>
        /// 获取数据库表结构
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="database"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ColumnInfo>> GetTableColsInfoAsync(IDbConnection conn, string tableName)
        {
            string sql;
            var database = GetDatabaseName(conn.ConnectionString);
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new Exception($"从连接字符串:{conn.ConnectionString}解析数据库名失败!");
            }
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(conn.ConnectionString);
            if (dbType == DatabaseType.SqlServer)
            {
                sql = $@"SELECT
                                  C.name 										as [ColumnCode]
                                 ,C.name 										as [ColumnName]
                                 ,T.name 										as [ColumnType]
                                 ,COLUMNPROPERTY(C.id,C.name,'PRECISION') 		as [ColumnLength]
                                 ,'textbox' as [ControlType] 
                                 ,convert(bit,case when exists(SELECT 1 FROM sysobjects where xtype='PK' and parent_obj=c.id and name in (
                                     SELECT name FROM sysindexes WHERE indid in(
                                         SELECT indid FROM sysindexkeys WHERE id = c.id AND colid=c.colid))) then 1 else 0 end) 
                                             									as [IsPK]
                                 ,convert(bit,C.IsNullable)  					as [IsNullable]
                                 ,ISNULL(CM.text,'') 							as [DefaultValue]
								 ,C.colid										as [OrderNo]
                            FROM syscolumns C
                            INNER JOIN systypes T ON C.xusertype = T.xusertype 
                            left JOIN sys.extended_properties ETP   ON  ETP.major_id = c.id AND ETP.minor_id = C.colid AND ETP.name ='MS_Description' 
                            left join syscomments CM on C.cdefault=CM.id
                            WHERE C.id = object_id('{tableName}')";
            }
            else if (dbType == DatabaseType.MySql)
            {
                sql = $@"select
                        column_name 									ColumnCode,
                        column_name 									ColumnName,
                        data_type 										ColumnType,
                        CASE data_type
                        WHEN 'varchar' THEN
	                        character_maximum_length
                      	WHEN 'nvarchar' THEN
                          character_maximum_length
                        WHEN 'bit' THEN
                          1
                        WHEN 'int' THEN
                          numeric_precision
                        WHEN 'datetime' THEN
                          datetime_precision
                        ELSE
	                        0
                        END 											ColumnLength,
                        CASE is_nullable  WHEN 'NO' then 0 ELSE 1 END  	IsNullable,
						CASE WHEN column_key='PRI' THEN 1 ELSE 0 END 	IsPK,
                        column_default 									DefaultValue,
						ORDINAL_POSITION 								OrderNo
                    from information_schema.columns where table_schema = '{database}' and table_name = '{tableName}' 
                    order by ORDINAL_POSITION asc";
            }
            else
            {
                sql = $@" SELECT 
                                    A.column_name       ColumnCode,
                                    A.column_name       ColumnName,
                                    A.data_type         ColumnType,
                                    (case A.data_type when 'NVARCHAR2' then to_char(A.CHAR_LENGTH) when 'NUMBER' then A.DATA_PRECISION||','||A.DATA_SCALE else to_char(A.data_length) end)      ColumnLength,
                                    CASE WHEN A.COLUMN_ID=1 THEN 1 ELSE 0 END IsPK, 
                                    CASE A.nullable 
                                        WHEN 'N' then 0
                                        ELSE 1
                                        END  IsNullable,
                                    A.Data_default      DefaultValue,
									A.COLUMN_ID		    OrderNo,
									B.comments		    Remark
                            from  user_tab_columns A,user_col_comments B
                            WHERE a.COLUMN_NAME=b.column_name and A.Table_Name = B.Table_Name and 
                                A.Table_Name=upper('{tableName}')
                            ORDER BY  A.COLUMN_ID ";
            }

            var result = await conn.QueryAsync<ColumnInfo>(sql);
            if (!result.Any())
            {
                throw new Exception($"{database}中没有找到{tableName}表");
            }
            // ColumnLength: Oracle如果是Number类型, ColumnLength值可能是"10,0"
            return result;
        }

        public static async Task<DataTable> QueryAsync(IDbConnection conn, string sql, object parameters, IDbTransaction transaction)
        {
            var dataTable = new DataTable();
            var reader = await conn.ExecuteReaderAsync(sql, param: parameters, transaction: transaction);
            dataTable.Load(reader);
            return dataTable;
        }

        public static bool AlreadyExistInTarget(DataRowCollection rows, DataRow sourceRow)
        {
            foreach (DataRow row in rows)
            {
                // 比较第一列的值(Id)
                if (row[0].ToString() == sourceRow[0].ToString())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>获取需要同步的DataColumn和DataRow</summary>
        public static SyncData GetSyncData(DataTable sourceDataTable, DataTable targetDataTable)
        {
            var columnNames = new List<string>();
            var sourceColumns = sourceDataTable.Columns;
            var targetColumns = targetDataTable.Columns;

            var insertingColumns = new List<DataColumn>();
            var deletingColumns = new List<DataColumn>();
            var targetColumnsCopied = new DataColumn[targetColumns.Count];
            targetColumns.CopyTo(targetColumnsCopied, 0);
            var targetColumnsCopiedList = targetColumnsCopied.ToList();
            foreach (DataColumn sourceColumn in sourceColumns)
            {
                columnNames.Add(sourceColumn.ColumnName);
                DataColumn targetCorrespondColumn = null;
                foreach (DataColumn targetColumn in targetColumns)
                {
                    if (targetColumn.ColumnName == sourceColumn.ColumnName)
                    {
                        targetCorrespondColumn = targetColumn;
                        break;
                    }
                }

                if (targetCorrespondColumn is not null)
                {
                    targetColumnsCopiedList.RemoveAll(col => col.ColumnName == targetCorrespondColumn.ColumnName);
                    if (sourceColumn.DataType.Name != targetCorrespondColumn.DataType.Name)
                    {
                        deletingColumns.Add(targetCorrespondColumn);
                        insertingColumns.Add(sourceColumn);
                    }
                }
                else
                {
                    insertingColumns.Add(sourceColumn);
                }
            }
            deletingColumns.AddRange(targetColumnsCopiedList);

            var sourceRows = sourceDataTable.Rows;
            var targetRows = targetDataTable.Rows;
            var insertingRows = new List<DataRow>();
            var updatingRows = new List<DataRow>();
            var targetRowsCopied = new DataRow[targetRows.Count];
            sourceRows.CopyTo(targetRowsCopied, 0);
            var deletingRows = targetRowsCopied.ToList();
            foreach (DataRow sourceRow in sourceRows)
            {
                DataRow targetCorrespondRow = null;
                foreach (DataRow targetRow in targetRowsCopied)
                {
                    if (sourceRow[0].ToString() == targetRow[0].ToString())
                    {
                        targetCorrespondRow = sourceRow;
                    }
                }
                if (targetCorrespondRow is not null)
                {
                    deletingRows.Remove(targetCorrespondRow);
                    if (deletingColumns.Any())
                    {
                        updatingRows.Add(sourceRow);
                    }
                    else
                    {
                        foreach (DataColumn sourceCol in sourceColumns)
                        {
                            if (sourceCol.DataType == typeof(byte[]) && (sourceRow[sourceCol.ColumnName] as byte[]).Length != (targetCorrespondRow[sourceCol.ColumnName] as byte[]).Length)
                            {
                                updatingRows.Add(sourceRow);
                            }
                            else if (sourceRow[sourceCol.ColumnName].ToString() != targetCorrespondRow[sourceCol.ColumnName].ToString())
                            {
                                updatingRows.Add(sourceRow);
                            }
                        }
                    }
                }
                else
                {
                    insertingRows.Add(sourceRow);
                }
            }

            return new SyncData(deletingColumns, insertingColumns, insertingRows, updatingRows, deletingRows);
        }
    }

    //SELECT a.BpmnBytes, a.ProcessImage FROM iduo_engine.devprocessmodel a; -- mediumblob
    //SELECT a.FieldConfigs FROM iduo_business.sys_datasource a; -- VARCHAR(3000)
    //SELECT a.ContentEn FROM iduo_portal.pt_joyrnalism a; -- mediumtext

    public class SyncData
    {
        public SyncData(List<DataColumn> deletingColumns, List<DataColumn> insertingColumns, List<DataRow> insertingRows, List<DataRow> updatingRows, List<DataRow> deletingRows)
        {
            InsertingColumns = insertingColumns;
        }
        public List<DataColumn> DeletingColumns { get; set; }
        public List<DataColumn> InsertingColumns { get; set; }

        public List<DataRow> InsertingRows { get; set; }
        public List<DataRow> UpdatingRows { get; set; }
        public List<DataRow> DeletingRows { get; set; }
    }
}

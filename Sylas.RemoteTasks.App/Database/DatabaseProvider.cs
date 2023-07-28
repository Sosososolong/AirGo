using System.Data.Common;
using System.Data;
using System.Data.SqlClient;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Utils;

namespace Sylas.RemoteTasks.App.Database;

public class DatabaseProvider : IDatabaseProvider
{
    public DatabaseProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        var defaultConnectionString = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(defaultConnectionString))
        {
            ConnectionString = defaultConnectionString;
        }
    }
    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    private readonly IConfiguration _configuration;

    public string? ConnectionString { get; set; }

    /// <summary>
    /// 获取与某个C#数据类型对应的在SQL server数据库中的类型
    /// </summary>
    /// <param name="t">C#中通过GetType()或typeof()获取某个数据的Type</param>
    /// <returns></returns>
    public object ConvertToLocalDbType(Type t)
    {
        return t.ToString() switch
        {
            "System.Boolean" => SqlDbType.Bit,
            "System.DateTime" => SqlDbType.DateTime,
            "System.Decimal" => SqlDbType.Decimal,
            "System.Single" => SqlDbType.Float,
            "System.Double" => SqlDbType.Float,
            "System.Byte[]" => SqlDbType.Image,
            "System.Int64" => SqlDbType.BigInt,
            "System.Int32" => SqlDbType.Int,
            "System.String" => SqlDbType.NVarChar,
            "System.Int16" => SqlDbType.SmallInt,
            "System.Byte" => SqlDbType.TinyInt,
            "System.Guid" => SqlDbType.UniqueIdentifier,
            "System.TimeSpan" => SqlDbType.Time,
            "System.Object" => SqlDbType.Variant,
            _ => (object)SqlDbType.Text,
        };
    }

    /// <summary>
    /// 执行sql语句,获取DataSet
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="commandType"></param>
    /// <param name="commandText"></param>
    /// <param name="dbParameters"></param>
    /// <returns></returns>
    public DataSet QueryDataset(DbConnection connection, CommandType commandType, string commandText, params DbParameter[] dbParameters)
    {
        //验证连接字符串
        if (connection == null)
        {
            throw new Exception("ConnectionString Empty");
        }
        
        DbCommand command = SqlClientFactory.Instance.CreateCommand();
        PrepareCommand(connection, command, null, commandType, commandText, dbParameters, out bool mustCloseConn);
        using DbDataAdapter adapter = SqlClientFactory.Instance.CreateDataAdapter();
        adapter.SelectCommand = command;
        DataSet dataSet = new();
        DateTime now = DateTime.Now;
        adapter.Fill(dataSet);
        DateTime dtEnd = DateTime.Now;
        command.Parameters.Clear();
        if (mustCloseConn)
        {
            connection.Close();
        }
        return dataSet;

    }

    /// <summary>
    /// 执行SQL语句之前的准备工作
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="command"></param>
    /// <param name="transaction"></param>
    /// <param name="commandType"></param>
    /// <param name="commandText"></param>
    /// <param name="commandParameters"></param>
    /// <param name="mustCloseConn"></param>
    private void PrepareCommand(DbConnection connection, DbCommand command, DbTransaction transaction, CommandType commandType, string commandText, DbParameter[] commandParameters, out bool mustCloseConn)
    {
        if (connection.State != ConnectionState.Open)
        {
            mustCloseConn = true;
            connection.Open();
        }
        else
        {
            mustCloseConn = false;
        }
        command.Connection = connection;
        command.CommandText = commandText;
        if (transaction != null)
        {
            if (transaction.Connection == null)
            {
                throw new Exception("The transaction was rollbacked or commited, please provide an open transaction.");
            }
            command.Transaction = transaction;
        }
        command.CommandType = commandType;
        if (commandParameters != null)
        {
            AttachParameters(command, commandParameters);
        }
    }

    private static void AttachParameters(DbCommand command, DbParameter[] commandParameters)
    {
        if (command == null)
        {
            throw new Exception("Command is null");
        }
        if (commandParameters != null)
        {
            foreach (DbParameter parameter in commandParameters)
            {
                if (parameter != null)
                {
                    if (((parameter.Direction == ParameterDirection.InputOutput) || (parameter.Direction == ParameterDirection.Input)) && (parameter.Value == null))
                    {
                        parameter.Value = DBNull.Value;
                    }
                    command.Parameters.Add(parameter);
                }
            }
        }
    }

    
    
    //...........................................................................................................
    public async Task<DataSet> QueryAsync(string sqlStr, Dictionary<string, object> parameters, string db = "")
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new Exception($"未指定需要连接的数据库: 数据库连接配置\"Default\"为空");
        }

        DbParameter[] dbPparameters = parameters.Select(x => CreateDbParameter(x.Key, x.Value)).ToArray();
        
        if (!string.IsNullOrWhiteSpace(db))
        {
            return await ExecuteQuerySqlAsync(sqlStr, dbPparameters, db);
        }
        
        return await ExecuteQuerySqlAsync(sqlStr, dbPparameters);
    }
    public async Task<DataSet> QueryAsyncWithConnectionString(string sqlStr, Dictionary<string, object> parameters, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"未指定需要连接的数据库: 参数\"connectiongString\"为空");
        }

        DbParameter[] dbPparameters = parameters.Select(x => CreateDbParameter(x.Key, x.Value)).ToArray();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            ConnectionString = connectionString;
        }
        
        return await ExecuteQuerySqlAsync(sqlStr, dbPparameters);
    }




    private async Task<DataSet> ExecuteQuerySqlAsync(string sqlStr, DbParameter[] parameters, string db = "")
    {
        //初始化 与数据库交互的一些对象
        DbConnection conn = SqlClientFactory.Instance.CreateConnection();
        if (!string.IsNullOrWhiteSpace(db))
        {
            await conn.ChangeDatabaseAsync(db);
        }

        return ExecuteQuerySqlAsync(conn, sqlStr, parameters);
    }

    private DataSet ExecuteQuerySqlAsync(DbConnection conn, string sqlStr, DbParameter[] parameters)
    {
        DbCommand command = SqlClientFactory.Instance.CreateCommand();

        conn.ConnectionString = ConnectionString;
        conn.Open();

        command.Connection = conn;
        command.CommandType = CommandType.Text;
        command.CommandText = sqlStr;

        if (parameters.Length > 0)
        {
            AttachParametersForSql(command, parameters);
        }

        DataSet dataSet = new();
        using (DbDataAdapter adapter = SqlClientFactory.Instance.CreateDataAdapter())
        {
            adapter.SelectCommand = command;
            adapter.Fill(dataSet);
            command.Parameters.Clear();
            conn.Close();
        }
        return dataSet;
    }

    /// <summary>
    /// 当SQL语句有非字符串参数的时候, 创建一个DbParameter参数对象, 不用指定参数的长度
    /// </summary>
    /// <param name="paramName">参数名称</param>
    /// <param name="paramValue">参数的值</param>
    /// <returns></returns>
    public DbParameter CreateDbParameter(string paramName, object paramValue)
    {
        SqlParameter parameter = new()
        {
            //设置参数名称
            ParameterName = paramName
        };
        //设置参数类型
        if (paramValue != null)
        {
            parameter.SqlDbType = (SqlDbType)ConvertToLocalDbType(paramValue.GetType());
        }
        //设置参数值
        parameter.Value = paramValue ?? DBNull.Value;
        //设置参数传入还是传出方向, 默认传入
        parameter.Direction = ParameterDirection.Input;
        return parameter;
    }

    /// <summary>
    /// 当SQL语句有字符串参数的时候, 创建一个DbParameter参数对象, 需指定参数长度, 可以让数据库重用执行计划, 提高SQL执行速度
    /// </summary>
    /// <param name="paramName"></param>
    /// <param name="paramValue"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public DbParameter CreateDbParameter(string paramName, object paramValue, int size)
    {
        SqlParameter parameter = new()
        {
            //设置参数名称
            ParameterName = "@" + paramName
        };
        //设置参数类型(不为空的情况)
        if (paramValue != null)
        {
            parameter.SqlDbType = (SqlDbType)ConvertToLocalDbType(paramValue.GetType());
        }
        //设置参数值
        parameter.Value = paramValue ?? DBNull.Value;
        //设置参数传入还是传出方向, 默认传入
        parameter.Direction = ParameterDirection.Input;
        //设置参数长度
        parameter.Size = size;
        return parameter;
    }

    /// <summary>
    /// 为SQL语句加上它所需要的参数
    /// </summary>
    /// <param name="command">由SqlClientFactory.Instance.CreateCommand()创建的DbCommand实例</param>
    /// <param name="parameters"></param>
    public void AttachParametersForSql(DbCommand command, DbParameter[] parameters)
    {
        foreach (DbParameter parameter in parameters)
        {
            if (parameter != null)
            {
                command.Parameters.Add(parameter);
            }
        }
    }


    /// <summary>
    /// 分页查询指定数据表, 可使用db参数切换到指定数据库
    /// </summary>
    /// <param name="table">查询的表明</param>
    /// <param name="pageIndex">第几页</param>
    /// <param name="pageSize">每页多少条数</param>
    /// <param name="orderField">排序字段</param>
    /// <param name="isAsc">是否升序</param>
    /// <param name="filters">查询条件</param>
    /// <param name="db">指定要切换查询的数据库, 不指定使用Default配置的数据库</param>
    /// <returns></returns>
    public async Task<PagedData<T>> QueryPagedDataAsync<T>(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, string db = "") where T : new()
    {
        //初始化 与数据库交互的一些对象
        DbConnection conn = SqlClientFactory.Instance.CreateConnection();
        if (!string.IsNullOrWhiteSpace(db))
        {
            await conn.ChangeDatabaseAsync(db);
        }

        string pagedSql = DatabaseInfo.GetPagedSql(conn.Database, table, DatabaseInfo.GetDbType(ConnectionString), pageIndex, pageSize, orderField, isAsc, filters, out string condition, out Dictionary<string, object> conditionParameters);
        string allCountSqlTxt = $"select count(*) from {conn.Database}.{table} where 1=1 {condition}";

        var dataSet = await QueryAsync(pagedSql, conditionParameters);
        var allCountDataSet = await QueryAsync(allCountSqlTxt, conditionParameters);

        var dt = dataSet.Tables[0];
        IEnumerable<T> data = dt.ToObjectList<T>();
        var allCount = Convert.ToInt32(allCountDataSet.Tables[0].Rows[0][0]);

        
        return new PagedData<T> { Data = data, Count = allCount, TotalPages = (allCount + pageSize - 1) / pageSize };
    }
    /// <summary>
    /// 分页查询指定数据表, 可使用数据库连接字符串connectionString参数指定连接的数据库
    /// </summary>
    /// <param name="table">查询的表明</param>
    /// <param name="pageIndex">第几页</param>
    /// <param name="pageSize">每页多少条数</param>
    /// <param name="orderField">排序字段</param>
    /// <param name="isAsc">是否升序</param>
    /// <param name="filters">查询条件</param>
    /// <param name="connectionString">指定要切换查询的数据库, 不指定使用Default配置的数据库连接</param>
    /// <returns></returns>
    public async Task<PagedData<T>> QueryPagedDataWithConnectionStringAsync<T>(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, string connectionString) where T : new()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("参数数据库连接字符串不能为空");
        }
        ConnectionString = connectionString;
        return await QueryPagedDataAsync<T>(table, pageIndex, pageSize, orderField, isAsc, filters);
    }
    /// <summary>
    /// 执行增删改的SQL语句 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <param name="db"></param>
    /// <returns></returns>
    public async Task<int> ExecuteScalarAsync(string sql, Dictionary<string, object> parameters, string db = "")
    {
        var dataSet = await QueryAsync(sql, parameters, db);
        return Convert.ToInt32(dataSet.Tables[0].Rows[0][0]);
    }
    /// <summary>
    /// 执行增删改的SQL语句 - 可使用数据库连接字符串指定数据库
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    public async Task<int> ExecuteScalarWithConnectionStringAsync(string sql, Dictionary<string, object> parameters, string connectionString)
    {
        var dataSet = await QueryAsyncWithConnectionString(sql, parameters, connectionString);
        return Convert.ToInt32(dataSet.Tables[0].Rows[0][0]);
    }
}

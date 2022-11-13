using System.Data.Common;
using System.Data;
using System.Data.SqlClient;

namespace Sylas.RemoteTasks.App.Database;

public class DatabaseProvider
{
    public DatabaseProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        ConnectionString = configuration.GetConnectionString("Default") ?? "server=192.168.1.100;uid=sa;pwd=123456;database=T10";
    }
    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    private readonly IConfiguration _configuration;

    public string ConnectionString { get; set; }

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
    public DataSet ExecuteDataset(DbConnection connection, CommandType commandType, string commandText, params DbParameter[] dbParameters)
    {
        //验证连接字符串
        if (connection == null)
        {
            throw new Exception("ConnectionString Empty");
        }
        
        DbCommand command = SqlClientFactory.Instance.CreateCommand();
        bool mustCloseConn = false;
        PrepareCommand(connection, command, null, commandType, commandText, dbParameters, out mustCloseConn);
        using (DbDataAdapter adapter = SqlClientFactory.Instance.CreateDataAdapter())
        {
            adapter.SelectCommand = command;
            DataSet dataSet = new DataSet();
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
            this.AttachParameters(command, commandParameters);
        }
    }

    private void AttachParameters(DbCommand command, DbParameter[] commandParameters)
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

    public DataSet ExecuteQuerySql(string sqlStr, DbParameter[] parameters)
    {
        //初始化 与数据库交互的一些对象
        DbConnection conn = SqlClientFactory.Instance.CreateConnection();
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

        DataSet dataSet = new DataSet();
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
        SqlParameter parameter = new SqlParameter();
        //设置参数名称
        parameter.ParameterName = paramName;
        //设置参数类型
        if (parameter != null)
        {
            parameter.SqlDbType = (SqlDbType)ConvertToLocalDbType(paramValue.GetType());
        }
        //设置参数值
        parameter.Value = paramValue == null ? DBNull.Value : paramValue;
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
        SqlParameter parameter = new SqlParameter();
        //设置参数名称
        parameter.ParameterName = "@" + paramName;
        //设置参数类型(不为空的情况)
        if (paramValue != null)
        {
            parameter.SqlDbType = (SqlDbType)(ConvertToLocalDbType(paramValue.GetType()));
        }
        //设置参数值
        parameter.Value = paramValue == null ? DBNull.Value : paramValue;
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
}

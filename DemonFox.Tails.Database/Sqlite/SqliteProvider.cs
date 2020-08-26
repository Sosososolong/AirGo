using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SQLite;
using System.Data;
using System.Data.Common;

namespace DemonFox.Tails.Database.Sqlite
{
    public class SqliteProvider
    {
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        private static string _connectionString;
        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new Exception("未设置数据库连接字符串");
                }
                return _connectionString;
            }
            set
            {
                _connectionString = value;
            }
        }
        /// <summary>
        /// 获取与某个C#数据类型对应的在SQL server数据库中的类型
        /// </summary>
        /// <param name="t">C#中通过GetType()或typeof()获取某个数据的Type</param>
        /// <returns></returns>
        public object ConvertToLocalDbType(Type t)
        {
            switch (t.ToString())
            {
                case "System.Boolean":
                    return SqlDbType.Bit;

                case "System.DateTime":
                    return SqlDbType.DateTime;

                case "System.Decimal":
                    return SqlDbType.Decimal;

                case "System.Single":
                    return SqlDbType.Float;

                case "System.Double":
                    return SqlDbType.Float;

                case "System.Byte[]":
                    return SqlDbType.Image;

                case "System.Int64":
                    return SqlDbType.BigInt;

                case "System.Int32":
                    return SqlDbType.Int;

                case "System.String":
                    return SqlDbType.NVarChar;

                case "System.Int16":
                    return SqlDbType.SmallInt;

                case "System.Byte":
                    return SqlDbType.TinyInt;

                case "System.Guid":
                    return SqlDbType.UniqueIdentifier;

                case "System.TimeSpan":
                    return SqlDbType.Time;

                case "System.Object":
                    return SqlDbType.Variant;
            }
            return SqlDbType.Int;
        }
        /// <summary>
        /// 执行sql语句,获取DataSet
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="dbParameters"></param>
        /// <returns></returns>
        public DataSet ExecuteDataset(SQLiteConnection connection, CommandType commandType, string commandText, SQLiteParameterCollection dbParameters)
        {
            //验证连接字符串
            if (connection == null)
            {
                throw new Exception("ConnectionString Empty");
            }

            SQLiteCommand command = new SQLiteCommand();
            bool mustCloseConn = false;
            PrepareCommand(connection, command, null, commandType, commandText, dbParameters, out mustCloseConn);
            
            using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
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
        private void PrepareCommand(DbConnection connection, DbCommand command, DbTransaction transaction, CommandType commandType, string commandText, SQLiteParameterCollection commandParameters, out bool mustCloseConn)
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
        private void AttachParameters(DbCommand command, SQLiteParameterCollection commandParameters)
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

        
    }
}

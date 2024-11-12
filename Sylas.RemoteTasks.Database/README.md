# Database Helper Classes
## DatabaseInfo
### supports MySql, SqlServer, PostgreSql, Oracle, Dm, and Sqlite databases.
### DatabaseInfo supports instantiation via dependency injection, connecting to the default database connection string configured in appsettings.json "ConnectionStrings:Default". If not found, an exception will be thrown.
### DatabaseInfo provides common static helper methods:
- `GetDbType`: Get the database type.
- `GetDbConnection(string connectionString)`: Get a database connection using the connection string (supports all supported database connection strings).
- `TransferDataAsync`: General data migration method.
- `GetDataTransferSqlInfosAsync`: Get SQL statement information for data migration (asynchronous iterator, suitable for large data volumes).
- `QueryPagedDataAsync`: General paginated data query.
- `GetPagedSql`: Get paginated SQL statements.
- `CompareRecordsAsync`: Data comparison (datasets are best provided as dictionary collections).
- `CreateTableIfNotExistAsync`: Create a table if it does not exist (table field information must be provided, supports querying from other databases using the `GetTableColumnsInfoAsync` method).
- `CreateTableAsync`: Create a table based on table field information.
- `GetCreateTableStatement`: Get the SQL statement to create a table.
- `GetTableColumnsInfoAsync`: Get table field information.
- `InsertDataAsync`: Insert specified datasets into the target table (the table must already exist).
- `GetInsertSqlInfosAsync`: Get SQL statement information for batch inserts.
- `GetAllTablesAsync`: Get all table names.
- `TableExistAsync`: Check if a table exists.
- `GetBatchInsertSql`: Generate SQL statements for batch inserts.
- `GetTableAllPkValuesAsync`: Get all primary key values of a table.
- Others...

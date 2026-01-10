using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using SmarterViews.Desktop.Models;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Service for retrieving database schema information (tables, views, columns)
/// </summary>
public class SchemaService
{
    /// <summary>
    /// Represents a database object (table or view)
    /// </summary>
    public class DatabaseObject
    {
        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "TABLE" or "VIEW"
        public List<ColumnInfo> Columns { get; set; } = new();
        
        public string DisplayName => string.IsNullOrEmpty(Schema) || Schema == "dbo" || Schema == "public" || Schema == "main"
            ? Name 
            : $"{Schema}.{Name}";
    }

    /// <summary>
    /// Represents a column in a database object
    /// </summary>
    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        
        public string DisplayName => $"{Name} ({DataType})";
    }

    /// <summary>
    /// Gets all tables and views from the database
    /// </summary>
    public async Task<List<DatabaseObject>> GetTablesAndViewsAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
    {
        var objects = new List<DatabaseObject>();
        
        await using var dbConnection = CreateConnection(connection.ConnectionString, connection.DatabaseType);
        await dbConnection.OpenAsync(cancellationToken);

        var sql = GetSchemaQuery(connection.DatabaseType);
        var results = await dbConnection.QueryAsync<dynamic>(sql);

        foreach (var row in results)
        {
            objects.Add(new DatabaseObject
            {
                Name = (string)row.TABLE_NAME,
                Schema = row.TABLE_SCHEMA?.ToString() ?? string.Empty,
                Type = ((string)row.TABLE_TYPE).Contains("VIEW") ? "VIEW" : "TABLE"
            });
        }

        return objects;
    }

    /// <summary>
    /// Gets columns for a specific table or view
    /// </summary>
    public async Task<List<ColumnInfo>> GetColumnsAsync(DatabaseConnection connection, string tableName, string? schema = null, CancellationToken cancellationToken = default)
    {
        var columns = new List<ColumnInfo>();
        
        await using var dbConnection = CreateConnection(connection.ConnectionString, connection.DatabaseType);
        await dbConnection.OpenAsync(cancellationToken);

        var sql = GetColumnsQuery(connection.DatabaseType, tableName, schema);
        var results = await dbConnection.QueryAsync<dynamic>(sql);

        foreach (var row in results)
        {
            columns.Add(new ColumnInfo
            {
                Name = (string)row.COLUMN_NAME,
                DataType = (string)row.DATA_TYPE,
                IsNullable = row.IS_NULLABLE?.ToString()?.ToUpper() == "YES" || row.IS_NULLABLE?.ToString() == "1"
            });
        }

        return columns;
    }

    /// <summary>
    /// Executes a raw SQL query and returns the results as a DataTable
    /// </summary>
    public async Task<DataTable> ExecuteQueryAsync(DatabaseConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        await using var dbConnection = CreateConnection(connection.ConnectionString, connection.DatabaseType);
        await dbConnection.OpenAsync(cancellationToken);

        var dataTable = new DataTable();
        
        using var reader = await dbConnection.ExecuteReaderAsync(sql);
        dataTable.Load(reader);

        return dataTable;
    }

    /// <summary>
    /// Executes a non-query SQL command and returns the number of affected rows
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(DatabaseConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        await using var dbConnection = CreateConnection(connection.ConnectionString, connection.DatabaseType);
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteAsync(sql);
    }

    private DbConnection CreateConnection(string connectionString, string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => new SqlConnection(connectionString),
            "MySql" => new MySqlConnection(connectionString),
            "PostgreSQL" => new NpgsqlConnection(connectionString),
            "SQLite" => new SqliteConnection(connectionString),
            "Oracle" => new OracleConnection(connectionString),
            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    private string GetSchemaQuery(string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => @"
                SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE 
                FROM INFORMATION_SCHEMA.TABLES 
                ORDER BY TABLE_TYPE, TABLE_SCHEMA, TABLE_NAME",
            
            "MySql" => @"
                SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = DATABASE()
                ORDER BY TABLE_TYPE, TABLE_NAME",
            
            "PostgreSQL" => @"
                SELECT table_schema AS TABLE_SCHEMA, table_name AS TABLE_NAME, table_type AS TABLE_TYPE
                FROM information_schema.tables 
                WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY table_type, table_schema, table_name",
            
            "SQLite" => @"
                SELECT '' AS TABLE_SCHEMA, name AS TABLE_NAME, type AS TABLE_TYPE 
                FROM sqlite_master 
                WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%'
                ORDER BY type, name",
            
            "Oracle" => @"
                SELECT OWNER AS TABLE_SCHEMA, TABLE_NAME, 'BASE TABLE' AS TABLE_TYPE 
                FROM ALL_TABLES 
                WHERE OWNER NOT IN ('SYS', 'SYSTEM', 'CTXSYS', 'MDSYS', 'XDB')
                UNION ALL
                SELECT OWNER AS TABLE_SCHEMA, VIEW_NAME AS TABLE_NAME, 'VIEW' AS TABLE_TYPE 
                FROM ALL_VIEWS 
                WHERE OWNER NOT IN ('SYS', 'SYSTEM', 'CTXSYS', 'MDSYS', 'XDB')
                ORDER BY TABLE_TYPE, TABLE_SCHEMA, TABLE_NAME",
            
            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    private string GetColumnsQuery(string databaseType, string tableName, string? schema)
    {
        return databaseType switch
        {
            "SqlServer" => string.IsNullOrEmpty(schema) || schema == "dbo"
                ? $@"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
                     FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_NAME = '{tableName}'
                     ORDER BY ORDINAL_POSITION"
                : $@"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
                     FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{tableName}'
                     ORDER BY ORDINAL_POSITION",
            
            "MySql" => $@"
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}'
                ORDER BY ORDINAL_POSITION",
            
            "PostgreSQL" => string.IsNullOrEmpty(schema) || schema == "public"
                ? $@"SELECT column_name AS COLUMN_NAME, data_type AS DATA_TYPE, is_nullable AS IS_NULLABLE 
                     FROM information_schema.columns 
                     WHERE table_name = '{tableName}'
                     ORDER BY ordinal_position"
                : $@"SELECT column_name AS COLUMN_NAME, data_type AS DATA_TYPE, is_nullable AS IS_NULLABLE 
                     FROM information_schema.columns 
                     WHERE table_schema = '{schema}' AND table_name = '{tableName}'
                     ORDER BY ordinal_position",
            
            "SQLite" => $"PRAGMA table_info('{tableName}')",
            
            "Oracle" => string.IsNullOrEmpty(schema)
                ? $@"SELECT COLUMN_NAME, DATA_TYPE, NULLABLE AS IS_NULLABLE 
                     FROM ALL_TAB_COLUMNS 
                     WHERE TABLE_NAME = '{tableName.ToUpper()}'
                     ORDER BY COLUMN_ID"
                : $@"SELECT COLUMN_NAME, DATA_TYPE, NULLABLE AS IS_NULLABLE 
                     FROM ALL_TAB_COLUMNS 
                     WHERE OWNER = '{schema.ToUpper()}' AND TABLE_NAME = '{tableName.ToUpper()}'
                     ORDER BY COLUMN_ID",
            
            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Parses SQLite PRAGMA table_info results into ColumnInfo objects
    /// </summary>
    public List<ColumnInfo> ParseSqlitePragmaResults(IEnumerable<dynamic> pragmaResults)
    {
        var columns = new List<ColumnInfo>();
        foreach (var row in pragmaResults)
        {
            columns.Add(new ColumnInfo
            {
                Name = (string)row.name,
                DataType = (string)row.type,
                IsNullable = (long)row.notnull == 0,
                IsPrimaryKey = (long)row.pk == 1
            });
        }
        return columns;
    }

    /// <summary>
    /// Generates a SELECT query for a table with a row limit
    /// </summary>
    public string GenerateSelectQuery(string tableName, string? schema, string databaseType, int limit = 1000)
    {
        var fullTableName = string.IsNullOrEmpty(schema) || schema == "dbo" || schema == "public" || schema == "main"
            ? QuoteIdentifier(tableName, databaseType)
            : $"{QuoteIdentifier(schema, databaseType)}.{QuoteIdentifier(tableName, databaseType)}";

        return databaseType switch
        {
            "SqlServer" => $"SELECT TOP {limit} * FROM {fullTableName}",
            "MySql" => $"SELECT * FROM {fullTableName} LIMIT {limit}",
            "PostgreSQL" => $"SELECT * FROM {fullTableName} LIMIT {limit}",
            "SQLite" => $"SELECT * FROM {fullTableName} LIMIT {limit}",
            "Oracle" => $"SELECT * FROM {fullTableName} WHERE ROWNUM <= {limit}",
            _ => $"SELECT * FROM {fullTableName}"
        };
    }

    private string QuoteIdentifier(string identifier, string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => $"[{identifier}]",
            "MySql" => $"`{identifier}`",
            "PostgreSQL" => $"\"{identifier}\"",
            "SQLite" => $"\"{identifier}\"",
            "Oracle" => $"\"{identifier.ToUpper()}\"",
            _ => identifier
        };
    }
}

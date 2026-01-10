using System.Data;
using System.Data.Common;
using Dapper;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Service for database access using Dapper
/// </summary>
public class DataService : IDisposable
{
    private readonly Func<DbConnection> _connectionFactory;
    private bool _disposed;

    public DataService(Func<DbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Executes a query and returns the results as a list of dynamic objects
    /// </summary>
    public async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        return await connection.QueryAsync(sql, parameters);
    }

    /// <summary>
    /// Executes a query and returns the results as a list of typed objects
    /// </summary>
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        return await connection.QueryAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes a query and returns the first result or default
    /// </summary>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes a scalar query
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Executes a command and returns the number of affected rows
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, parameters);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

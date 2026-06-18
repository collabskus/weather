using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Weather.Infrastructure.Caching;

/// <summary>Creates open <see cref="SqliteConnection"/> instances from configuration.</summary>
public interface ISqliteConnectionFactory
{
    /// <summary>Create and open a new connection. The caller owns disposal.</summary>
    Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class SqliteConnectionFactory(IOptions<SqliteCacheOptions> options) : ISqliteConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

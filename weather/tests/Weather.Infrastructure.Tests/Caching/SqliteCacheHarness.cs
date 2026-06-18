using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Infrastructure.Caching;

namespace Weather.Infrastructure.Tests.Caching;

/// <summary>
/// Creates a throwaway, file-backed SQLite database initialised with the real
/// production schema (via <see cref="DatabaseInitializer"/>), so cache tests run
/// against genuine SQLite rather than a mock. Each instance uses a unique file
/// and deletes it (plus the WAL side-files) on disposal.
/// </summary>
internal sealed class SqliteCacheHarness : IAsyncDisposable
{
    public string DatabasePath { get; }

    public SqliteConnectionFactory Factory { get; }

    private SqliteCacheHarness(string databasePath, SqliteConnectionFactory factory)
    {
        DatabasePath = databasePath;
        Factory = factory;
    }

    public static async Task<SqliteCacheHarness> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"weather-cache-tests-{Guid.NewGuid():N}.db");
        var options = Options.Create(new SqliteCacheOptions { ConnectionString = $"Data Source={path}" });
        var factory = new SqliteConnectionFactory(options);

        var initializer = new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance);
        await initializer.StartAsync(CancellationToken.None);

        return new SqliteCacheHarness(path, factory);
    }

    public async ValueTask DisposeAsync()
    {
        // Return pooled connections so the OS file handle is released first.
        SqliteConnection.ClearAllPools();
        await Task.Yield();

        foreach (var file in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // A throwaway temp file; ignore if the OS still holds it briefly.
            }
        }
    }
}

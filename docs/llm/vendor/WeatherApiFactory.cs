using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weather.Api.Tests.Fakes;
using Weather.Core.Abstractions;

namespace Weather.Api.Tests;

/// <summary>
/// Boots the real API in-memory for end-to-end tests. Two things are swapped:
/// the NWS client (replaced with <see cref="FakeNwsApiClient"/> so no network is
/// touched) and the cache connection string (pointed at a unique temp file so
/// the genuine SQLite cache path runs, isolated per factory).
/// </summary>
internal sealed class WeatherApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"weather-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:ConnectionString"] = $"Data Source={_databasePath}",
                // Keep tests offline: never attempt to export telemetry to Uptrace.
                ["Uptrace:Enabled"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INwsApiClient>();
            services.AddSingleton<INwsApiClient, FakeNwsApiClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
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
                // Throwaway temp file.
            }
        }
    }
}

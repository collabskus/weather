using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// Creates the cache schema on startup. Idempotent: every statement uses
/// <c>IF NOT EXISTS</c>, so it is safe to run on every boot. Enables
/// Write-Ahead Logging for better read/write concurrency under load.
/// </summary>
public sealed class DatabaseInitializer(
    ISqliteConnectionFactory connectionFactory,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    internal const string SchemaSql =
        """
        PRAGMA journal_mode = WAL;
        PRAGMA busy_timeout = 5000;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS PointMetadata (
            CoordinateKey     TEXT    NOT NULL PRIMARY KEY,
            Latitude          REAL    NOT NULL,
            Longitude         REAL    NOT NULL,
            GridId            TEXT    NOT NULL,
            GridX             INTEGER NOT NULL,
            GridY             INTEGER NOT NULL,
            ForecastUrl       TEXT    NOT NULL,
            ForecastHourlyUrl TEXT    NOT NULL,
            City              TEXT    NULL,
            State             TEXT    NULL,
            TimeZone          TEXT    NULL,
            RadarStation      TEXT    NULL,
            RetrievedAtUtc    TEXT    NOT NULL,
            ExpiresAtUtc      TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS GridForecast (
            GridId         TEXT    NOT NULL,
            GridX          INTEGER NOT NULL,
            GridY          INTEGER NOT NULL,
            Payload        TEXT    NOT NULL,
            ETag           TEXT    NULL,
            GeneratedAt    TEXT    NOT NULL,
            UpdateTime     TEXT    NOT NULL,
            RetrievedAtUtc TEXT    NOT NULL,
            ExpiresAtUtc   TEXT    NOT NULL,
            PRIMARY KEY (GridId, GridX, GridY)
        );

        CREATE INDEX IF NOT EXISTS IX_GridForecast_ExpiresAtUtc
            ON GridForecast (ExpiresAtUtc);

        -- Per-cell "extras": hourly forecast + latest observation + cell centre,
        -- stored as one JSON blob. Followed-link data for the full area view.
        CREATE TABLE IF NOT EXISTS CellExtras (
            GridId         TEXT    NOT NULL,
            GridX          INTEGER NOT NULL,
            GridY          INTEGER NOT NULL,
            Payload        TEXT    NOT NULL,
            RetrievedAtUtc TEXT    NOT NULL,
            ExpiresAtUtc   TEXT    NOT NULL,
            PRIMARY KEY (GridId, GridX, GridY)
        );

        CREATE INDEX IF NOT EXISTS IX_CellExtras_ExpiresAtUtc
            ON CellExtras (ExpiresAtUtc);
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        logger.LogInformation("SQLite cache schema initialised (WAL enabled).");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

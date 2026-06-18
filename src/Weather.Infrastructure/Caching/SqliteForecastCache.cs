using System.Text.Json;
using Dapper;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Infrastructure.Serialization;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// SQLite-backed store of short-lived forecast payloads, keyed by grid cell.
/// The full domain <see cref="Forecast"/> is serialised to a JSON column;
/// ETag and TTL bookkeeping live in dedicated columns for cheap inspection.
/// </summary>
internal sealed class SqliteForecastCache(ISqliteConnectionFactory connectionFactory) : IForecastCache
{
    private const string SelectSql =
        """
        SELECT Payload, ETag, RetrievedAtUtc, ExpiresAtUtc
        FROM GridForecast
        WHERE GridId = @GridId AND GridX = @GridX AND GridY = @GridY;
        """;

    private const string UpsertSql =
        """
        INSERT INTO GridForecast
            (GridId, GridX, GridY, Payload, ETag, GeneratedAt, UpdateTime, RetrievedAtUtc, ExpiresAtUtc)
        VALUES
            (@GridId, @GridX, @GridY, @Payload, @ETag, @GeneratedAt, @UpdateTime, @RetrievedAtUtc, @ExpiresAtUtc)
        ON CONFLICT(GridId, GridX, GridY) DO UPDATE SET
            Payload        = excluded.Payload,
            ETag           = excluded.ETag,
            GeneratedAt    = excluded.GeneratedAt,
            UpdateTime     = excluded.UpdateTime,
            RetrievedAtUtc = excluded.RetrievedAtUtc,
            ExpiresAtUtc   = excluded.ExpiresAtUtc;
        """;

    public async Task<CachedForecast?> GetAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<ForecastRow>(
            new CommandDefinition(SelectSql, new { grid.GridId, grid.GridX, grid.GridY },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var forecast = JsonSerializer.Deserialize<Forecast>(row.Payload, WeatherJson.Options);
        if (forecast is null)
        {
            return null;
        }

        return new CachedForecast(
            forecast,
            row.ETag,
            TimestampText.Parse(row.RetrievedAtUtc),
            TimestampText.Parse(row.ExpiresAtUtc));
    }

    public async Task UpsertAsync(CachedForecast entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(entry.Forecast, WeatherJson.Options);
        var grid = entry.Forecast.Grid;

        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            grid.GridId,
            grid.GridX,
            grid.GridY,
            Payload = payload,
            entry.ETag,
            GeneratedAt = TimestampText.ToText(entry.Forecast.GeneratedAt),
            UpdateTime = TimestampText.ToText(entry.Forecast.UpdateTime),
            RetrievedAtUtc = TimestampText.ToText(entry.RetrievedAtUtc),
            ExpiresAtUtc = TimestampText.ToText(entry.ExpiresAtUtc),
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record ForecastRow(string Payload, string? ETag, string RetrievedAtUtc, string ExpiresAtUtc);
}

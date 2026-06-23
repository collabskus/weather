using System.Text.Json;
using Dapper;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Infrastructure.Serialization;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// SQLite-backed store of active NWS alerts, keyed by the rounded query
/// coordinate. The whole alert list is one JSON column; TTL bookkeeping lives
/// in dedicated columns. Alerts are point-based (they apply to the whole
/// neighbourhood), so unlike the forecast/extras caches this is keyed by
/// coordinate, not by grid cell. This is what stops every browser refresh from
/// hammering <c>/alerts/active</c>: a fresh entry is served straight from
/// SQLite, exactly like metadata and forecasts.
/// </summary>
internal sealed class SqliteAlertCache(ISqliteConnectionFactory connectionFactory) : IAlertCache
{
    private const string SelectSql =
        """
        SELECT Payload, RetrievedAtUtc, ExpiresAtUtc
        FROM Alerts
        WHERE CoordinateKey = @CoordinateKey;
        """;

    private const string UpsertSql =
        """
        INSERT INTO Alerts
            (CoordinateKey, Payload, RetrievedAtUtc, ExpiresAtUtc)
        VALUES
            (@CoordinateKey, @Payload, @RetrievedAtUtc, @ExpiresAtUtc)
        ON CONFLICT(CoordinateKey) DO UPDATE SET
            Payload        = excluded.Payload,
            RetrievedAtUtc = excluded.RetrievedAtUtc,
            ExpiresAtUtc   = excluded.ExpiresAtUtc;
        """;

    public async Task<CachedAlerts?> GetAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var key = coordinate.Rounded().ToCacheKey();

        var row = await connection.QuerySingleOrDefaultAsync<AlertRow>(
            new CommandDefinition(SelectSql, new { CoordinateKey = key },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var alerts = JsonSerializer.Deserialize<List<WeatherAlert>>(row.Payload, WeatherJson.Options);
        if (alerts is null)
        {
            return null;
        }

        return new CachedAlerts(
            alerts,
            TimestampText.Parse(row.RetrievedAtUtc),
            TimestampText.Parse(row.ExpiresAtUtc));
    }

    public async Task UpsertAsync(GeoCoordinate coordinate, CachedAlerts entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var key = coordinate.Rounded().ToCacheKey();

        // Materialise to a concrete list so the JSON shape is stable regardless
        // of the underlying IReadOnlyList implementation handed in.
        var payload = JsonSerializer.Serialize(entry.Alerts.ToList(), WeatherJson.Options);

        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            CoordinateKey = key,
            Payload = payload,
            RetrievedAtUtc = TimestampText.ToText(entry.RetrievedAtUtc),
            ExpiresAtUtc = TimestampText.ToText(entry.ExpiresAtUtc),
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record AlertRow(string Payload, string RetrievedAtUtc, string ExpiresAtUtc);
}

using System.Text.Json;
using Dapper;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Infrastructure.Serialization;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// SQLite-backed store of per-cell "extras" (hourly forecast + latest
/// observation + cell centre), keyed by grid cell. The whole bundle is one
/// JSON column; TTL bookkeeping lives in dedicated columns. Kept separate from
/// <see cref="SqliteForecastCache"/> so the headline daily forecast keeps its
/// own conditional-GET / TTL policy untouched.
/// </summary>
internal sealed class SqliteCellExtrasCache(ISqliteConnectionFactory connectionFactory) : ICellExtrasCache
{
    private const string SelectSql =
        """
        SELECT Payload, RetrievedAtUtc, ExpiresAtUtc
        FROM CellExtras
        WHERE GridId = @GridId AND GridX = @GridX AND GridY = @GridY;
        """;

    private const string UpsertSql =
        """
        INSERT INTO CellExtras
            (GridId, GridX, GridY, Payload, RetrievedAtUtc, ExpiresAtUtc)
        VALUES
            (@GridId, @GridX, @GridY, @Payload, @RetrievedAtUtc, @ExpiresAtUtc)
        ON CONFLICT(GridId, GridX, GridY) DO UPDATE SET
            Payload        = excluded.Payload,
            RetrievedAtUtc = excluded.RetrievedAtUtc,
            ExpiresAtUtc   = excluded.ExpiresAtUtc;
        """;

    public async Task<CachedCellExtras?> GetAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<ExtrasRow>(
            new CommandDefinition(SelectSql, new { grid.GridId, grid.GridX, grid.GridY },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var extras = JsonSerializer.Deserialize<CellExtras>(row.Payload, WeatherJson.Options);
        if (extras is null)
        {
            return null;
        }

        return new CachedCellExtras(
            extras,
            TimestampText.Parse(row.RetrievedAtUtc),
            TimestampText.Parse(row.ExpiresAtUtc));
    }

    public async Task UpsertAsync(GridPoint grid, CachedCellExtras entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(entry.Extras, WeatherJson.Options);

        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            grid.GridId,
            grid.GridX,
            grid.GridY,
            Payload = payload,
            RetrievedAtUtc = TimestampText.ToText(entry.RetrievedAtUtc),
            ExpiresAtUtc = TimestampText.ToText(entry.ExpiresAtUtc),
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record ExtrasRow(string Payload, string RetrievedAtUtc, string ExpiresAtUtc);
}

using Dapper;
using Weather.Core.Abstractions;
using Weather.Core.Models;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// SQLite-backed store of long-lived coordinate-&gt;grid metadata. The lookup
/// key is the coordinate rounded to four decimals, so physically-close users
/// collapse onto a single row and a single upstream <c>/points</c> call.
/// </summary>
internal sealed class SqlitePointMetadataCache(ISqliteConnectionFactory connectionFactory) : IPointMetadataCache
{
    private const string SelectSql =
        """
        SELECT Latitude, Longitude, GridId, GridX, GridY, ForecastUrl, ForecastHourlyUrl,
               City, State, TimeZone, RadarStation, RetrievedAtUtc, ExpiresAtUtc
        FROM PointMetadata
        WHERE CoordinateKey = @CoordinateKey;
        """;

    private const string UpsertSql =
        """
        INSERT INTO PointMetadata
            (CoordinateKey, Latitude, Longitude, GridId, GridX, GridY, ForecastUrl, ForecastHourlyUrl,
             City, State, TimeZone, RadarStation, RetrievedAtUtc, ExpiresAtUtc)
        VALUES
            (@CoordinateKey, @Latitude, @Longitude, @GridId, @GridX, @GridY, @ForecastUrl, @ForecastHourlyUrl,
             @City, @State, @TimeZone, @RadarStation, @RetrievedAtUtc, @ExpiresAtUtc)
        ON CONFLICT(CoordinateKey) DO UPDATE SET
            Latitude          = excluded.Latitude,
            Longitude         = excluded.Longitude,
            GridId            = excluded.GridId,
            GridX             = excluded.GridX,
            GridY             = excluded.GridY,
            ForecastUrl       = excluded.ForecastUrl,
            ForecastHourlyUrl = excluded.ForecastHourlyUrl,
            City              = excluded.City,
            State             = excluded.State,
            TimeZone          = excluded.TimeZone,
            RadarStation      = excluded.RadarStation,
            RetrievedAtUtc    = excluded.RetrievedAtUtc,
            ExpiresAtUtc      = excluded.ExpiresAtUtc;
        """;

    public async Task<CachedPointMetadata?> GetAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var key = coordinate.Rounded().ToCacheKey();
        var row = await connection.QuerySingleOrDefaultAsync<MetadataRow>(
            new CommandDefinition(SelectSql, new { CoordinateKey = key },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var metadata = new PointMetadata(
            Query: new GeoCoordinate(row.Latitude, row.Longitude),
            Grid: new GridPoint(row.GridId, (int)row.GridX, (int)row.GridY),
            ForecastUrl: row.ForecastUrl,
            ForecastHourlyUrl: row.ForecastHourlyUrl,
            City: row.City,
            State: row.State,
            TimeZone: row.TimeZone,
            RadarStation: row.RadarStation);

        return new CachedPointMetadata(
            metadata,
            TimestampText.Parse(row.RetrievedAtUtc),
            TimestampText.Parse(row.ExpiresAtUtc));
    }

    public async Task UpsertAsync(CachedPointMetadata entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var coordinate = entry.Metadata.Query.Rounded();
        var grid = entry.Metadata.Grid;

        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            CoordinateKey = coordinate.ToCacheKey(),
            coordinate.Latitude,
            coordinate.Longitude,
            grid.GridId,
            grid.GridX,
            grid.GridY,
            entry.Metadata.ForecastUrl,
            entry.Metadata.ForecastHourlyUrl,
            entry.Metadata.City,
            entry.Metadata.State,
            entry.Metadata.TimeZone,
            entry.Metadata.RadarStation,
            RetrievedAtUtc = TimestampText.ToText(entry.RetrievedAtUtc),
            ExpiresAtUtc = TimestampText.ToText(entry.ExpiresAtUtc),
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // SQLite stores GridX/GridY in INTEGER columns, which Microsoft.Data.Sqlite
    // surfaces as Int64. Dapper's constructor-based record materialization is
    // strict about width, so these MUST be `long` (not `int`) or it throws
    // "no matching constructor". They are narrowed back to the domain's int on
    // the way out, in GetAsync — NWS grid indices are small (< a few hundred).
    private sealed record MetadataRow(
        double Latitude,
        double Longitude,
        string GridId,
        long GridX,
        long GridY,
        string ForecastUrl,
        string ForecastHourlyUrl,
        string? City,
        string? State,
        string? TimeZone,
        string? RadarStation,
        string RetrievedAtUtc,
        string ExpiresAtUtc);
}

using Dapper;
using Weather.Core.Abstractions;
using Weather.Core.Models;

namespace Weather.Infrastructure.Caching;

/// <summary>
/// SQLite-backed negative cache for forecast 404s, keyed by grid cell. Unlike
/// the forecast/extras/alert caches (which serialise a nested payload to a JSON
/// column), the RFC 7807 problem is flat, so its fields are stored in dedicated
/// columns — cheap to inspect and queryable, e.g. to count how many marine
/// cells have been discovered (<c>SELECT COUNT(*) FROM ForecastNotFound WHERE
/// ProblemType LIKE '%MarineForecastNotSupported'</c>). This is what stops the
/// area fan-out and background warmer from re-requesting cells NWS does not
/// cover: a fresh row is served straight from SQLite, exactly like the positive
/// caches.
/// </summary>
internal sealed class SqliteForecastNegativeCache(ISqliteConnectionFactory connectionFactory) : IForecastNegativeCache
{
    private const string SelectSql =
        """
        SELECT ProblemType, ProblemTitle, ProblemStatus, ProblemDetail, CorrelationId,
               RetrievedAtUtc, ExpiresAtUtc
        FROM ForecastNotFound
        WHERE GridId = @GridId AND GridX = @GridX AND GridY = @GridY;
        """;

    private const string UpsertSql =
        """
        INSERT INTO ForecastNotFound
            (GridId, GridX, GridY, ProblemType, ProblemTitle, ProblemStatus, ProblemDetail,
             CorrelationId, RetrievedAtUtc, ExpiresAtUtc)
        VALUES
            (@GridId, @GridX, @GridY, @ProblemType, @ProblemTitle, @ProblemStatus, @ProblemDetail,
             @CorrelationId, @RetrievedAtUtc, @ExpiresAtUtc)
        ON CONFLICT(GridId, GridX, GridY) DO UPDATE SET
            ProblemType    = excluded.ProblemType,
            ProblemTitle   = excluded.ProblemTitle,
            ProblemStatus  = excluded.ProblemStatus,
            ProblemDetail  = excluded.ProblemDetail,
            CorrelationId  = excluded.CorrelationId,
            RetrievedAtUtc = excluded.RetrievedAtUtc,
            ExpiresAtUtc   = excluded.ExpiresAtUtc;
        """;

    private const string DeleteSql =
        """
        DELETE FROM ForecastNotFound
        WHERE GridId = @GridId AND GridX = @GridX AND GridY = @GridY;
        """;

    public async Task<CachedForecastNotFound?> GetAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<NotFoundRow>(
            new CommandDefinition(SelectSql, new { grid.GridId, grid.GridX, grid.GridY },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        // A row with every problem column null means "404 with no body".
        var problem =
            row.ProblemType is null && row.ProblemTitle is null && row.ProblemStatus is null &&
            row.ProblemDetail is null && row.CorrelationId is null
                ? null
                : new NwsProblem(
                    row.ProblemType,
                    row.ProblemTitle,
                    row.ProblemStatus is { } status ? (int)status : null,
                    row.ProblemDetail,
                    row.CorrelationId);

        return new CachedForecastNotFound(
            problem,
            TimestampText.Parse(row.RetrievedAtUtc),
            TimestampText.Parse(row.ExpiresAtUtc));
    }

    public async Task UpsertAsync(GridPoint grid, CachedForecastNotFound entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            grid.GridId,
            grid.GridX,
            grid.GridY,
            ProblemType = entry.Problem?.Type,
            ProblemTitle = entry.Problem?.Title,
            ProblemStatus = entry.Problem?.Status,
            ProblemDetail = entry.Problem?.Detail,
            CorrelationId = entry.Problem?.CorrelationId,
            RetrievedAtUtc = TimestampText.ToText(entry.RetrievedAtUtc),
            ExpiresAtUtc = TimestampText.ToText(entry.ExpiresAtUtc),
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RemoveAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(DeleteSql,
            new { grid.GridId, grid.GridX, grid.GridY },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // SQLite stores ProblemStatus in an INTEGER column, which Microsoft.Data.Sqlite
    // surfaces as Int64. Dapper's constructor-based record materialization is strict
    // about width, so this MUST be `long?` (not `int?`) or it throws "no matching
    // constructor". It is narrowed back to the domain's int? in GetAsync.
    private sealed record NotFoundRow(
        string? ProblemType,
        string? ProblemTitle,
        long? ProblemStatus,
        string? ProblemDetail,
        string? CorrelationId,
        string RetrievedAtUtc,
        string ExpiresAtUtc);
}

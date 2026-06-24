using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>
/// Negative ("not-found") cache for the headline daily forecast, keyed by grid
/// cell. When NWS answers <c>/gridpoints/.../forecast</c> with a 404 — for a
/// marine/coastal cell this is <c>MarineForecastNotSupported</c> — that answer
/// is structurally stable: the cell simply has no land forecast. Remembering it
/// for a TTL is what stops the area fan-out (and the background warmer) from
/// re-requesting the same uncovered cells on every view.
///
/// <para>
/// Kept separate from <see cref="IForecastCache"/> on purpose. The positive
/// cache stores a payload with conditional-GET/ETag bookkeeping; this stores
/// only the <em>absence</em> of a forecast plus the captured
/// <see cref="NwsProblem"/>. Mixing the two would force the positive cache's
/// record to model "no forecast", weakening it. The TTL (see
/// <c>NwsClientOptions.NotFoundForecastTtl</c>) is deliberately longer than the
/// forecast TTL but finite, so a misrouted or transient 404 self-heals on the
/// next check and a cell that NWS later starts supporting is picked up.
/// </para>
/// </summary>
public interface IForecastNegativeCache
{
    /// <summary>The remembered 404 for a cell, or <c>null</c> if none is stored.</summary>
    Task<CachedForecastNotFound?> GetAsync(GridPoint grid, CancellationToken cancellationToken = default);

    /// <summary>Remember that a cell currently has no forecast, with TTL bookkeeping.</summary>
    Task UpsertAsync(GridPoint grid, CachedForecastNotFound entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forget a cell's remembered 404. Called when a later fetch succeeds, so a
    /// cell that flips from uncovered to covered does not keep a dead row.
    /// </summary>
    Task RemoveAsync(GridPoint grid, CancellationToken cancellationToken = default);
}

/// <summary>
/// A remembered forecast 404 for a grid cell: the captured problem detail (may
/// be <c>null</c> when NWS sent no body) plus the TTL window during which the
/// cell is treated as having no forecast without asking NWS again.
/// </summary>
public sealed record CachedForecastNotFound(
    NwsProblem? Problem,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsFresh(DateTimeOffset now) => ExpiresAtUtc > now;
}

using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>
/// Cache of the per-cell "extras" (hourly forecast + latest observation +
/// cell centre) that the area view follows links to fetch. Kept separate from
/// <see cref="IForecastCache"/> so the headline daily forecast retains its own
/// conditional-GET / stale-on-error policy untouched.
/// </summary>
public interface ICellExtrasCache
{
    Task<CachedCellExtras?> GetAsync(GridPoint grid, CancellationToken cancellationToken = default);

    Task UpsertAsync(GridPoint grid, CachedCellExtras entry, CancellationToken cancellationToken = default);
}

/// <summary>Cell extras as stored in the cache, with TTL bookkeeping.</summary>
public sealed record CachedCellExtras(
    CellExtras Extras,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsFresh(DateTimeOffset now) => ExpiresAtUtc > now;
}

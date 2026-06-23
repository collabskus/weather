using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>
/// Cache of active NWS alerts keyed by the (rounded) query coordinate. Alerts
/// are point-based and apply to the whole neighbourhood, so they are cached
/// once per coordinate rather than per grid cell. Kept separate from the
/// forecast and extras caches because alerts have their own (shorter) freshness
/// policy: they can appear and clear quickly, but not so quickly that every
/// browser refresh should hit NWS.
/// </summary>
public interface IAlertCache
{
    Task<CachedAlerts?> GetAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    Task UpsertAsync(GeoCoordinate coordinate, CachedAlerts entry, CancellationToken cancellationToken = default);
}

/// <summary>Active alerts for a coordinate as stored in the cache, with TTL bookkeeping.</summary>
public sealed record CachedAlerts(
    IReadOnlyList<WeatherAlert> Alerts,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsFresh(DateTimeOffset now) => ExpiresAtUtc > now;
}

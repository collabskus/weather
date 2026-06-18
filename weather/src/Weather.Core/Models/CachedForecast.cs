namespace Weather.Core.Models;

/// <summary>A forecast as stored in the cache, with HTTP ETag and TTL bookkeeping.</summary>
public sealed record CachedForecast(
    Forecast Forecast,
    string? ETag,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsFresh(DateTimeOffset now) => ExpiresAtUtc > now;
}

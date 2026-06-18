namespace Weather.Core.Models;

/// <summary>Point metadata as stored in the cache, with TTL bookkeeping.</summary>
public sealed record CachedPointMetadata(
    PointMetadata Metadata,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsFresh(DateTimeOffset now) => ExpiresAtUtc > now;
}

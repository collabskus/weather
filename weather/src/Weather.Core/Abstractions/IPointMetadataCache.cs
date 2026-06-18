using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>Cache of long-lived coordinate-&gt;grid metadata.</summary>
public interface IPointMetadataCache
{
    Task<CachedPointMetadata?> GetAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    Task UpsertAsync(CachedPointMetadata entry, CancellationToken cancellationToken = default);
}

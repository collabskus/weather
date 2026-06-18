using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>Cache of short-lived forecast payloads, keyed by grid cell.</summary>
public interface IForecastCache
{
    Task<CachedForecast?> GetAsync(GridPoint grid, CancellationToken cancellationToken = default);

    Task UpsertAsync(CachedForecast entry, CancellationToken cancellationToken = default);
}

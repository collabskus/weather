using Weather.Core.Models;

namespace Weather.Api.Contracts;

/// <summary>
/// API response for a neighbourhood query: the primary cell plus any adjacent
/// cells that were already warm in the cache.
/// </summary>
public sealed record NeighborhoodResponse(
    double Latitude,
    double Longitude,
    ForecastResponse Primary,
    IReadOnlyList<ForecastResponse> Neighbors)
{
    public static NeighborhoodResponse FromDomain(NeighborhoodForecast neighborhood)
    {
        var neighbors = new List<ForecastResponse>(neighborhood.Neighbors.Count);
        foreach (var neighbor in neighborhood.Neighbors)
        {
            neighbors.Add(ForecastResponse.FromDomain(neighbor));
        }

        return new NeighborhoodResponse(
            neighborhood.Query.Latitude,
            neighborhood.Query.Longitude,
            ForecastResponse.FromDomain(neighborhood.Primary),
            neighbors);
    }
}

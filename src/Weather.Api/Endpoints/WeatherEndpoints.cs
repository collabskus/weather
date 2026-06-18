using Microsoft.AspNetCore.Http.HttpResults;
using Weather.Api.Contracts;
using Weather.Core.Abstractions;
using Weather.Core.Models;

namespace Weather.Api.Endpoints;

/// <summary>
/// Minimal-API endpoints for forecasts. Coordinate validation happens here at
/// the edge; everything else is delegated to <see cref="IWeatherService"/>,
/// for which caching and resilience are an implementation detail.
/// </summary>
public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/forecast").WithTags("Forecast");

        group.MapGet("/", GetForecastAsync)
            .WithName("GetForecast")
            .WithSummary("Forecast for the grid cell containing a coordinate.")
            .WithDescription("Resolves the coordinate to an NWS grid cell and returns its forecast, served from cache when fresh.");

        group.MapGet("/neighborhood", GetNeighborhoodForecastAsync)
            .WithName("GetNeighborhoodForecast")
            .WithSummary("Forecast for a coordinate plus any already-warm adjacent cells.")
            .WithDescription("Returns the primary cell immediately and includes neighbouring cells only if they were already warmed in the background.");

        return endpoints;
    }

    private static async Task<Results<Ok<ForecastResponse>, ProblemHttpResult, NotFound>> GetForecastAsync(
        double latitude,
        double longitude,
        IWeatherService weatherService,
        CancellationToken cancellationToken)
    {
        if (!GeoCoordinate.IsValid(latitude, longitude))
        {
            return InvalidCoordinates();
        }

        var forecast = await weatherService
            .GetForecastAsync(new GeoCoordinate(latitude, longitude), cancellationToken)
            .ConfigureAwait(false);

        return forecast is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ForecastResponse.FromDomain(forecast));
    }

    private static async Task<Results<Ok<NeighborhoodResponse>, ProblemHttpResult, NotFound>> GetNeighborhoodForecastAsync(
        double latitude,
        double longitude,
        IWeatherService weatherService,
        CancellationToken cancellationToken)
    {
        if (!GeoCoordinate.IsValid(latitude, longitude))
        {
            return InvalidCoordinates();
        }

        var neighborhood = await weatherService
            .GetNeighborhoodForecastAsync(new GeoCoordinate(latitude, longitude), cancellationToken)
            .ConfigureAwait(false);

        return neighborhood is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(NeighborhoodResponse.FromDomain(neighborhood));
    }

    private static ProblemHttpResult InvalidCoordinates() => TypedResults.Problem(
        title: "Invalid coordinates",
        detail: "Latitude must be between -90 and 90 and longitude between -180 and 180.",
        statusCode: StatusCodes.Status400BadRequest);
}

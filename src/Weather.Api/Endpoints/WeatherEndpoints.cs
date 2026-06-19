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
    // Cap the neighbourhood radius an HTTP caller can request (1 => 8 cells,
    // 2 => 24). Bounds the worst-case upstream fan-out for a cold area.
    private const int MaxRadius = 3;

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

        group.MapGet("/area", GetAreaForecastAsync)
            .WithName("GetAreaForecast")
            .WithSummary("The user's cell and every neighbouring cell — ordered by distance — with all of their data.")
            .WithDescription("Resolves the coordinate to a grid cell, then returns that cell plus every cell within the requested radius (ordered by distance from the user), each with its daily forecast, hourly forecast and latest observation, along with any active alerts. Every cell is served cache-aside.");

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

    private static async Task<Results<Ok<AreaResponse>, ProblemHttpResult, NotFound>> GetAreaForecastAsync(
        double latitude,
        double longitude,
        IWeatherService weatherService,
        CancellationToken cancellationToken,
        int radius = 1)
    {
        if (!GeoCoordinate.IsValid(latitude, longitude))
        {
            return InvalidCoordinates();
        }

        if (radius is < 1 or > MaxRadius)
        {
            return TypedResults.Problem(
                title: "Invalid radius",
                detail: $"Radius must be between 1 and {MaxRadius}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var area = await weatherService
            .GetAreaForecastAsync(new GeoCoordinate(latitude, longitude), radius, cancellationToken)
            .ConfigureAwait(false);

        return area is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(AreaResponse.FromDomain(area));
    }

    private static ProblemHttpResult InvalidCoordinates() => TypedResults.Problem(
        title: "Invalid coordinates",
        detail: "Latitude must be between -90 and 90 and longitude between -180 and 180.",
        statusCode: StatusCodes.Status400BadRequest);
}

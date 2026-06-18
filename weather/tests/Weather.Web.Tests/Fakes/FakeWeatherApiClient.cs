namespace Weather.Web.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IWeatherApiClient"/>. Returns a configured
/// forecast (or <c>null</c> for "no coverage"), or throws to simulate a network
/// error. Records the last coordinates it was asked about.
/// </summary>
internal sealed class FakeWeatherApiClient : IWeatherApiClient
{
    private readonly ForecastDto? _forecast;
    private readonly Exception? _toThrow;

    private FakeWeatherApiClient(ForecastDto? forecast, Exception? toThrow)
    {
        _forecast = forecast;
        _toThrow = toThrow;
    }

    public double? LastLatitude { get; private set; }

    public double? LastLongitude { get; private set; }

    public static FakeWeatherApiClient Returning(ForecastDto? forecast) => new(forecast, null);

    public static FakeWeatherApiClient Throwing(Exception toThrow) => new(null, toThrow);

    public Task<ForecastDto?> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        LastLatitude = latitude;
        LastLongitude = longitude;

        return _toThrow is not null
            ? Task.FromException<ForecastDto?>(_toThrow)
            : Task.FromResult(_forecast);
    }

    public Task<NeighborhoodDto?> GetNeighborhoodAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<NeighborhoodDto?>(null);
}

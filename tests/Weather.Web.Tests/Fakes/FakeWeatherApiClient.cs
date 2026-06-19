namespace Weather.Web.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IWeatherApiClient"/>. The dashboard now drives the
/// full <c>GetAreaAsync</c> path, so the fake returns a configured
/// <see cref="AreaDto"/> (or <c>null</c> for "no coverage"), or throws to
/// simulate a network error. Records the last coordinates it was asked about.
/// The forecast/neighbourhood members remain so the interface is satisfied, but
/// the component does not call them.
/// </summary>
internal sealed class FakeWeatherApiClient : IWeatherApiClient
{
    private readonly AreaDto? _area;
    private readonly Exception? _toThrow;

    private FakeWeatherApiClient(AreaDto? area, Exception? toThrow)
    {
        _area = area;
        _toThrow = toThrow;
    }

    public double? LastLatitude { get; private set; }

    public double? LastLongitude { get; private set; }

    public static FakeWeatherApiClient Returning(AreaDto? area) => new(area, null);

    public static FakeWeatherApiClient Throwing(Exception toThrow) => new(null, toThrow);

    public Task<AreaDto?> GetAreaAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        LastLatitude = latitude;
        LastLongitude = longitude;

        return _toThrow is not null
            ? Task.FromException<AreaDto?>(_toThrow)
            : Task.FromResult(_area);
    }

    public Task<ForecastDto?> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<ForecastDto?>(_area?.Primary is { } cell
            ? new ForecastDto(cell.GridId, cell.GridX, cell.GridY, cell.GeneratedAt, cell.UpdateTime, cell.Periods)
            : null);

    public Task<NeighborhoodDto?> GetNeighborhoodAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<NeighborhoodDto?>(null);
}

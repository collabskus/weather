using Weather.Core.Abstractions;
using Weather.Infrastructure.Caching;

namespace Weather.Infrastructure.Tests.Caching;

public sealed class SqliteCellExtrasCacheTests
{
    private static readonly GridPoint Grid = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static CellExtras SampleExtras() => new(
        Hourly: new[]
        {
            new ForecastPeriod(1, string.Empty, Now, Now.AddHours(1), true, 90, "F", 2,
                "6 mph", "S", "Sunny", string.Empty, "icon-1"),
            new ForecastPeriod(2, string.Empty, Now.AddHours(1), Now.AddHours(2), true, 89, "F", 2,
                "7 mph", "S", "Sunny", string.Empty, "icon-2"),
        },
        Observation: new Observation(
            "KPHF", "Newport News", Now, "Sunny", "icon-obs",
            88, 70, 55, 8, 15, "S", 30.06, 10.0),
        Center: new GeoCoordinate(37.088, -76.452));

    [Test]
    public async Task UpsertThenGetRoundTripsEveryField()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteCellExtrasCache(harness.Factory);
        var entry = new CachedCellExtras(SampleExtras(), Now, Now.AddMinutes(30));

        await cache.UpsertAsync(Grid, entry);
        var read = await cache.GetAsync(Grid);

        read.ShouldNotBeNull();
        read!.RetrievedAtUtc.ShouldBe(Now);
        read.ExpiresAtUtc.ShouldBe(Now.AddMinutes(30));
        read.Extras.Hourly.Count.ShouldBe(2);
        read.Extras.Hourly[0].Temperature.ShouldBe(90);
        read.Extras.Observation.ShouldNotBeNull();
        read.Extras.Observation!.TemperatureF.ShouldBe(88);
        read.Extras.Observation.WindDirection.ShouldBe("S");
        read.Extras.Center.ShouldNotBeNull();
        read.Extras.Center!.Value.Latitude.ShouldBe(37.088, tolerance: 0.0001);
    }

    [Test]
    public async Task GetReturnsNullWhenAbsent()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteCellExtrasCache(harness.Factory);

        (await cache.GetAsync(Grid)).ShouldBeNull();
    }

    [Test]
    public async Task UpsertOverwritesTheExistingEntry()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteCellExtrasCache(harness.Factory);

        await cache.UpsertAsync(Grid, new CachedCellExtras(SampleExtras(), Now, Now.AddMinutes(30)));

        var updated = SampleExtras() with { Observation = null };
        await cache.UpsertAsync(Grid, new CachedCellExtras(updated, Now.AddMinutes(5), Now.AddMinutes(35)));

        var read = await cache.GetAsync(Grid);
        read.ShouldNotBeNull();
        read!.Extras.Observation.ShouldBeNull();
        read.RetrievedAtUtc.ShouldBe(Now.AddMinutes(5));
    }

    [Test]
    public async Task FreshnessTracksTheExpiryInstant()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteCellExtrasCache(harness.Factory);
        await cache.UpsertAsync(Grid, new CachedCellExtras(SampleExtras(), Now, Now.AddMinutes(30)));

        var read = await cache.GetAsync(Grid);

        read!.IsFresh(Now.AddMinutes(10)).ShouldBeTrue();
        read.IsFresh(Now.AddMinutes(31)).ShouldBeFalse();
    }
}

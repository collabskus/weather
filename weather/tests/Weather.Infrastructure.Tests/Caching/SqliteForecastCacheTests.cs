using Weather.Infrastructure.Caching;

namespace Weather.Infrastructure.Tests.Caching;

public sealed class SqliteForecastCacheTests
{
    private static readonly GridPoint Grid = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static Forecast BuildForecast(DateTimeOffset generatedAt, string firstShort = "Sunny", int firstTemp = 90) =>
        new(
            Grid,
            generatedAt,
            generatedAt.AddMinutes(-30),
            new[]
            {
                new ForecastPeriod(1, "This Afternoon", generatedAt, generatedAt.AddHours(4),
                    true, firstTemp, "F", 3, "5 to 12 mph", "S", firstShort,
                    "Detailed afternoon text.", "icon-day"),
                new ForecastPeriod(2, "Tonight", generatedAt.AddHours(4), generatedAt.AddHours(16),
                    false, 74, "F", null, "12 mph", "S", "Mostly Clear",
                    "Detailed night text.", "icon-night"),
            });

    [Test]
    public async Task Upsert_then_get_round_trips_the_forecast()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastCache(harness.Factory);

        var forecast = BuildForecast(Now);
        var entry = new CachedForecast(forecast, "\"etag-1\"", Now, Now.AddHours(1));

        await cache.UpsertAsync(entry);
        var loaded = await cache.GetAsync(Grid);

        loaded.ShouldNotBeNull();
        loaded!.ETag.ShouldBe("\"etag-1\"");
        loaded.RetrievedAtUtc.ShouldBe(Now);
        loaded.ExpiresAtUtc.ShouldBe(Now.AddHours(1));
        loaded.Forecast.Grid.ShouldBe(Grid);
        loaded.Forecast.Periods.Count.ShouldBe(2);
        loaded.Forecast.Periods[0].Temperature.ShouldBe(90);
        loaded.Forecast.Periods[0].ShortForecast.ShouldBe("Sunny");
        loaded.Forecast.Periods[1].ProbabilityOfPrecipitation.ShouldBeNull();
        loaded.IsFresh(Now).ShouldBeTrue();
    }

    [Test]
    public async Task Get_returns_null_for_a_missing_grid()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastCache(harness.Factory);

        (await cache.GetAsync(new GridPoint("OKX", 33, 35))).ShouldBeNull();
    }

    [Test]
    public async Task Expired_entry_is_returned_but_reports_not_fresh()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastCache(harness.Factory);

        var entry = new CachedForecast(BuildForecast(Now), "\"old\"", Now.AddHours(-2), Now.AddHours(-1));
        await cache.UpsertAsync(entry);

        var loaded = await cache.GetAsync(Grid);

        loaded.ShouldNotBeNull();
        loaded!.IsFresh(Now).ShouldBeFalse();
    }

    [Test]
    public async Task Upsert_overwrites_an_existing_entry_for_the_same_grid()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastCache(harness.Factory);

        await cache.UpsertAsync(new CachedForecast(
            BuildForecast(Now, firstShort: "Sunny", firstTemp: 90), "\"v1\"", Now, Now.AddHours(1)));
        await cache.UpsertAsync(new CachedForecast(
            BuildForecast(Now.AddHours(1), firstShort: "Cloudy", firstTemp: 70), "\"v2\"", Now.AddHours(1), Now.AddHours(2)));

        var loaded = await cache.GetAsync(Grid);

        loaded.ShouldNotBeNull();
        loaded!.ETag.ShouldBe("\"v2\"");
        loaded.Forecast.Periods[0].ShortForecast.ShouldBe("Cloudy");
        loaded.Forecast.Periods[0].Temperature.ShouldBe(70);
    }
}

namespace Weather.Core.Tests;

public sealed class ForecastFetchResultTests
{
    private static Forecast SampleForecast() => new(
        new GridPoint("AKQ", 83, 61),
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        Array.Empty<ForecastPeriod>());

    [Test]
    public void Success_carries_forecast_etag_and_maxage()
    {
        var forecast = SampleForecast();

        var result = ForecastFetchResult.Success(forecast, "\"abc\"", TimeSpan.FromMinutes(30));

        result.Outcome.ShouldBe(NwsFetchOutcome.Success);
        result.Forecast.ShouldBe(forecast);
        result.ETag.ShouldBe("\"abc\"");
        result.MaxAge.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void NotModified_carries_no_forecast()
    {
        var result = ForecastFetchResult.NotModified("\"abc\"", TimeSpan.FromMinutes(10));

        result.Outcome.ShouldBe(NwsFetchOutcome.NotModified);
        result.Forecast.ShouldBeNull();
        result.ETag.ShouldBe("\"abc\"");
        result.MaxAge.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Test]
    public void NotFound_is_a_terminal_outcome_with_no_payload()
    {
        ForecastFetchResult.NotFound.Outcome.ShouldBe(NwsFetchOutcome.NotFound);
        ForecastFetchResult.NotFound.Forecast.ShouldBeNull();
        ForecastFetchResult.NotFound.ETag.ShouldBeNull();
    }

    [Test]
    public void Unavailable_is_a_terminal_outcome_with_no_payload()
    {
        ForecastFetchResult.Unavailable.Outcome.ShouldBe(NwsFetchOutcome.Unavailable);
        ForecastFetchResult.Unavailable.Forecast.ShouldBeNull();
        ForecastFetchResult.Unavailable.ETag.ShouldBeNull();
    }
}

public sealed class GridPointTests
{
    [Test]
    public void ToString_is_office_slash_x_comma_y() =>
        new GridPoint("AKQ", 83, 61).ToString().ShouldBe("AKQ/83,61");

    [Test]
    public void Records_with_the_same_values_are_equal() =>
        new GridPoint("AKQ", 83, 61).ShouldBe(new GridPoint("AKQ", 83, 61));

    [Test]
    public void Records_with_different_values_are_not_equal() =>
        new GridPoint("AKQ", 83, 61).ShouldNotBe(new GridPoint("AKQ", 83, 62));

    [Test]
    public void With_expression_replaces_a_single_index()
    {
        var origin = new GridPoint("AKQ", 83, 61);

        (origin with { GridX = 84 }).ShouldBe(new GridPoint("AKQ", 84, 61));
    }
}

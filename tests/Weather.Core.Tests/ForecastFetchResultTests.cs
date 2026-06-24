namespace Weather.Core.Tests;

public sealed class ForecastFetchResultTests
{
    private static Forecast SampleForecast() => new(
        new GridPoint("AKQ", 83, 61),
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        []);

    [Test]
    public void SuccessCarriesForecastEtagAndMaxage()
    {
        var forecast = SampleForecast();

        var result = ForecastFetchResult.Success(forecast, "\"abc\"", TimeSpan.FromMinutes(30));

        result.Outcome.ShouldBe(NwsFetchOutcome.Success);
        result.Forecast.ShouldBe(forecast);
        result.ETag.ShouldBe("\"abc\"");
        result.MaxAge.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void NotModifiedCarriesNoForecast()
    {
        var result = ForecastFetchResult.NotModified("\"abc\"", TimeSpan.FromMinutes(10));

        result.Outcome.ShouldBe(NwsFetchOutcome.NotModified);
        result.Forecast.ShouldBeNull();
        result.ETag.ShouldBe("\"abc\"");
        result.MaxAge.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Test]
    public void NotFoundIsATerminalOutcomeWithNoPayload()
    {
        var result = ForecastFetchResult.NotFound();

        result.Outcome.ShouldBe(NwsFetchOutcome.NotFound);
        result.Forecast.ShouldBeNull();
        result.ETag.ShouldBeNull();
        result.Problem.ShouldBeNull();
    }

    [Test]
    public void NotFoundCarriesTheProblemDetailWhenSupplied()
    {
        var problem = new NwsProblem(
            "https://api.weather.gov/problems/MarineForecastNotSupported",
            "Marine Forecast Not Supported",
            404,
            "Forecasts for marine areas are not yet supported by this API.",
            "1d604a85");

        var result = ForecastFetchResult.NotFound(problem);

        result.Outcome.ShouldBe(NwsFetchOutcome.NotFound);
        result.Problem.ShouldBe(problem);
        result.Problem!.TypeName.ShouldBe("MarineForecastNotSupported");
    }

    [Test]
    public void UnavailableIsATerminalOutcomeWithNoPayload()
    {
        ForecastFetchResult.Unavailable.Outcome.ShouldBe(NwsFetchOutcome.Unavailable);
        ForecastFetchResult.Unavailable.Forecast.ShouldBeNull();
        ForecastFetchResult.Unavailable.ETag.ShouldBeNull();
    }
}

public sealed class GridPointTests
{
    [Test]
    public void ToStringIsOfficeSlashXCommaY() =>
        new GridPoint("AKQ", 83, 61).ToString().ShouldBe("AKQ/83,61");

    [Test]
    public void RecordsWithTheSameValuesAreEqual() =>
        new GridPoint("AKQ", 83, 61).ShouldBe(new GridPoint("AKQ", 83, 61));

    [Test]
    public void RecordsWithDifferentValuesAreNotEqual() =>
        new GridPoint("AKQ", 83, 61).ShouldNotBe(new GridPoint("AKQ", 83, 62));

    [Test]
    public void WithExpressionReplacesASingleIndex()
    {
        var origin = new GridPoint("AKQ", 83, 61);

        (origin with { GridX = 84 }).ShouldBe(new GridPoint("AKQ", 84, 61));
    }
}

public sealed class NwsProblemTests
{
    [Test]
    public void TypeNameIsTheLastPathSegmentOfTheTypeUri() =>
        new NwsProblem("https://api.weather.gov/problems/MarineForecastNotSupported", null, null, null, null)
            .TypeName.ShouldBe("MarineForecastNotSupported");

    [Test]
    public void TypeNameIgnoresATrailingSlash() =>
        new NwsProblem("https://api.weather.gov/problems/InvalidPoint/", null, null, null, null)
            .TypeName.ShouldBe("InvalidPoint");

    [Test]
    public void TypeNameReturnsTheWholeValueWhenThereIsNoSlash() =>
        new NwsProblem("BareType", null, null, null, null).TypeName.ShouldBe("BareType");

    [Test]
    public void TypeNameIsNullWhenTypeIsMissing() =>
        new NwsProblem(null, "Some title", 404, null, null).TypeName.ShouldBeNull();
}

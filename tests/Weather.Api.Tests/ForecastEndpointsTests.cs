using System.Net;
using System.Net.Http.Json;

namespace Weather.Api.Tests;

public sealed class ForecastEndpointsTests : IDisposable
{
    private readonly WeatherApiFactory _factory = new();

    [Test]
    public async Task GetForecastWithValidCoordinatesReturns200AndAForecast()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/forecast?latitude=37.0879&longitude=-76.4505");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiForecast>();
        body.ShouldNotBeNull();
        body!.GridId.ShouldBe("AKQ");
        body.Periods.ShouldNotBeEmpty();
        body.Periods[0].Temperature.ShouldBe(90);
        body.Periods[0].ShortForecast.ShouldBe("Sunny");
    }

    [Test]
    public async Task GetForecastWithInvalidLatitudeReturns400()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/forecast?latitude=200&longitude=-76.4505");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetForecastForAnUncoveredLocationReturns404()
    {
        using var client = _factory.CreateClient();

        // (0, 0) — open ocean — is treated as outside NWS coverage by the fake.
        using var response = await client.GetAsync("/api/forecast?latitude=0&longitude=0");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetNeighborhoodWithValidCoordinatesReturns200()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/forecast/neighborhood?latitude=37.0879&longitude=-76.4505");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiNeighborhood>();
        body.ShouldNotBeNull();
        body!.Primary.GridId.ShouldBe("AKQ");
    }

    public void Dispose() => _factory.Dispose();

    // Minimal mirrors of the API's camelCase JSON contract for assertions.
    private sealed record ApiForecast(string GridId, int GridX, int GridY, IReadOnlyList<ApiPeriod> Periods);

    private sealed record ApiPeriod(int Temperature, string ShortForecast);

    private sealed record ApiNeighborhood(double Latitude, double Longitude, ApiForecast Primary);
}

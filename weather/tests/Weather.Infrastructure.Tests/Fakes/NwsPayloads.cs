namespace Weather.Infrastructure.Tests.Fakes;

/// <summary>
/// Canned NWS API responses, trimmed to the fields the client consumes but
/// otherwise shaped exactly like the real <c>application/geo+json</c> payloads
/// (top-level <c>properties</c> envelope, <c>probabilityOfPrecipitation</c> as
/// <c>{ unitCode, value }</c>, camelCase keys).
/// </summary>
internal static class NwsPayloads
{
    /// <summary>A <c>/points/37.0879,-76.4505</c> response resolving to grid AKQ/83,61.</summary>
    public const string Points =
        """
        {
          "properties": {
            "gridId": "AKQ",
            "gridX": 83,
            "gridY": 61,
            "forecast": "https://api.weather.gov/gridpoints/AKQ/83,61/forecast",
            "forecastHourly": "https://api.weather.gov/gridpoints/AKQ/83,61/forecast/hourly",
            "timeZone": "America/New_York",
            "radarStation": "KAKQ",
            "relativeLocation": {
              "type": "Feature",
              "properties": {
                "city": "Bethel Manor",
                "state": "VA"
              }
            }
          }
        }
        """;

    /// <summary>A two-period <c>/gridpoints/AKQ/83,61/forecast</c> response.</summary>
    public const string Forecast =
        """
        {
          "properties": {
            "generatedAt": "2026-06-17T18:37:33+00:00",
            "updateTime": "2026-06-17T17:09:58+00:00",
            "periods": [
              {
                "number": 1,
                "name": "This Afternoon",
                "startTime": "2026-06-17T14:00:00-04:00",
                "endTime": "2026-06-17T18:00:00-04:00",
                "isDaytime": true,
                "temperature": 90,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": 3 },
                "windSpeed": "5 to 12 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/day/few?size=medium",
                "shortForecast": "Sunny",
                "detailedForecast": "Sunny, with a high near 90. South wind 5 to 12 mph."
              },
              {
                "number": 2,
                "name": "Tonight",
                "startTime": "2026-06-17T18:00:00-04:00",
                "endTime": "2026-06-18T06:00:00-04:00",
                "isDaytime": false,
                "temperature": 74,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": null },
                "windSpeed": "12 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/night/few?size=medium",
                "shortForecast": "Mostly Clear",
                "detailedForecast": "Mostly clear, with a low around 74. South wind around 12 mph."
              }
            ]
          }
        }
        """;
}

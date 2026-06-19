# Weather.Web

The front end: a **Blazor Server** dashboard. It asks the browser for the user's
location, sends the coordinate to `Weather.Api`, and renders the full area view —
a sky-state hero, the latest observation, an hourly strip, the daily cards, every
neighbouring tile (distance-ordered, collapsed by default), and any active
alerts. The dashboard never reasons about caching; that lives in the API. The
visual behaviour is detailed in [`../../docs/FRONTEND.md`](../../docs/FRONTEND.md).

## Geolocation: a bridge that never rejects

Browser geolocation is reached through `IGeolocationService` (so the component is
testable without a browser). The JavaScript bridge in `wwwroot/js/geolocation.js`
**always resolves** a typed result object and **never rejects** — "the user said
no" is a normal value, not an exception. Each outcome maps to a distinct
`GeolocationError` and a distinct UI state, all of which offer **manual
latitude/longitude entry** plus a one-click sample location:

| Outcome | State | What the user sees |
| --- | --- | --- |
| Coordinates returned | success | The area view for their tile and neighbours |
| Permission denied | `PermissionDenied` | "Location is off" + manual entry + sample |
| Position unavailable / timeout / unsupported | `PositionUnavailable` | "Couldn't pin you down" + manual entry + sample |
| Coordinate outside NWS coverage | (API `404`) | "No coverage for that spot" + manual entry + sample |
| API / network error | (exception) | "The forecast didn't load" + retry + sample |

So permission denial and unavailability are ordinary code paths, each with a
clear fallback, rather than error cases.

## Contents

- **Components/Pages/`Home.razor`** — the dashboard and its state machine
  (initializing → requesting location → loading → loaded, plus the denied /
  unavailable / not-covered / error fallbacks). Rendered declaratively with stable
  `data-testid` hooks; all formatting uses `CultureInfo.InvariantCulture`.
- **Services/** — `GeolocationService` (+ `IGeolocationService`), the typed
  `WeatherApiClient` (+ `IWeatherApiClient`) with its view-model DTOs
  (`WeatherViewModels`), and `SkyPalette`, which derives the hero gradient and a
  readable foreground from the period's day/night flag and short-forecast keywords.
- `Program.cs` registers the geolocation service and the typed API client. Under
  Aspire the `https+http://api` scheme is resolved by service discovery; standalone
  runs set `WeatherApi:BaseUrl`.

`tests/Weather.Web.Tests` render the state machine, every fallback, the area view,
and the palette with bUnit.

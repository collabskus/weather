using Microsoft.JSInterop;

namespace Weather.Web.Services;

/// <summary>
/// Bridges to <c>wwwroot/js/geolocation.js</c>, whose <c>getCurrentPosition</c>
/// resolves a plain object in every case (success or failure) and never
/// rejects. This keeps the C# side branch-only: no exception handling for the
/// ordinary "user said no" path. JS-bridge faults (circuit disconnected, script
/// missing) are still caught and surfaced as <see cref="GeolocationError.Unknown"/>.
/// </summary>
internal sealed class GeolocationService(IJSRuntime jsRuntime) : IGeolocationService
{
    public async ValueTask<GeolocationResult> GetCurrentPositionAsync(
        CancellationToken cancellationToken = default)
    {
        GeoJsResult raw;
        try
        {
            raw = await jsRuntime
                .InvokeAsync<GeoJsResult>("weatherGeo.getCurrentPosition", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            return GeolocationResult.Failed(GeolocationError.Unknown);
        }
        catch (JSDisconnectedException)
        {
            return GeolocationResult.Failed(GeolocationError.Unknown);
        }
        catch (OperationCanceledException)
        {
            return GeolocationResult.Failed(GeolocationError.Unknown);
        }

        if (raw.Success && raw.Latitude is { } latitude && raw.Longitude is { } longitude)
        {
            return new GeolocationResult(true, latitude, longitude, raw.Accuracy, GeolocationError.None);
        }

        var error = raw.ErrorCode switch
        {
            "PermissionDenied" => GeolocationError.PermissionDenied,
            "PositionUnavailable" => GeolocationError.PositionUnavailable,
            "Timeout" => GeolocationError.Timeout,
            "NotSupported" => GeolocationError.NotSupported,
            _ => GeolocationError.Unknown,
        };

        return GeolocationResult.Failed(error);
    }

    /// <summary>Shape of the object resolved by the JS helper.</summary>
    private sealed record GeoJsResult(
        bool Success,
        double? Latitude,
        double? Longitude,
        double? Accuracy,
        string? ErrorCode);
}

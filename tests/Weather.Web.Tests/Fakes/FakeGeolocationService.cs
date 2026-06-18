namespace Weather.Web.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IGeolocationService"/>. Either returns a fixed
/// result immediately, or — when gated — blocks until the test completes the
/// supplied <see cref="TaskCompletionSource{TResult}"/>, so the component's
/// "requesting location" state can be observed before a result arrives.
/// </summary>
internal sealed class FakeGeolocationService : IGeolocationService
{
    private readonly GeolocationResult _result;
    private readonly TaskCompletionSource<GeolocationResult>? _gate;

    private FakeGeolocationService(GeolocationResult result, TaskCompletionSource<GeolocationResult>? gate)
    {
        _result = result;
        _gate = gate;
    }

    public static FakeGeolocationService Returning(GeolocationResult result) => new(result, null);

    public static (FakeGeolocationService Service, TaskCompletionSource<GeolocationResult> Gate) Gated()
    {
        var gate = new TaskCompletionSource<GeolocationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        // GeolocationResult is a reference type, so `default` would be null and
        // violate the non-nullable parameter. On the gated path the stored
        // result is never read (the gate's Task is awaited instead), so this is
        // an inert placeholder.
        var placeholder = GeolocationResult.Failed(GeolocationError.Unknown);
        return (new FakeGeolocationService(placeholder, gate), gate);
    }

    public async ValueTask<GeolocationResult> GetCurrentPositionAsync(CancellationToken cancellationToken = default)
    {
        if (_gate is not null)
        {
            return await _gate.Task.ConfigureAwait(false);
        }

        return _result;
    }
}

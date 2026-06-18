namespace Weather.Infrastructure.Tests.Fakes;

/// <summary>
/// A <see cref="TimeProvider"/> whose "now" is set by the test and can be
/// advanced, so freshness/TTL logic can be exercised without real waiting.
/// </summary>
internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset value) => _now = value;
}

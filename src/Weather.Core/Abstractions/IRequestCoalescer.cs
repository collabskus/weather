namespace Weather.Core.Abstractions;

/// <summary>
/// In-process "single-flight" coordinator. When many callers ask for the same
/// <typeparamref name="TKey"/> at the same time, only the FIRST runs the
/// supplied factory; every other concurrent caller awaits and shares that one
/// result. This is the defence against a cache <em>stampede</em> (a.k.a.
/// "dogpile" / "thundering herd"): without it, every request that arrives
/// during a cache-fill window is an independent upstream call.
/// </summary>
/// <remarks>
/// <para>
/// The contract is deliberately tight: the coalescer guarantees that no more
/// than one factory invocation per key is in flight at any instant. It makes
/// NO promises about caching across time — once a flight completes, the entry
/// is removed and the next caller starts a fresh flight. Durable freshness is
/// the cache's job; collapsing concurrent work is the coalescer's job.
/// </para>
/// <para>
/// Implementations must be thread-safe and must not leak entries: a completed,
/// faulted or cancelled flight is always removed so a later caller is never
/// handed a stale, failed task.
/// </para>
/// </remarks>
public interface IRequestCoalescer<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Run <paramref name="factory"/> for <paramref name="key"/>, or — if a
    /// flight for that key is already running — await and return its result
    /// instead of starting a second one.
    /// </summary>
    /// <param name="key">The coalescing key (e.g. a grid cell).</param>
    /// <param name="factory">
    /// Produces the value for a cold key. Invoked at most once per concurrent
    /// flight. It should observe the <see cref="CancellationToken"/> it is
    /// given, which is a token shared by the flight (see remarks on cancellation
    /// in the implementation).
    /// </param>
    /// <param name="cancellationToken">
    /// The calling request's token. Cancelling it detaches THIS caller from the
    /// flight; the shared flight itself continues for the remaining callers.
    /// </param>
    Task<TValue> RunAsync<TValue>(
        TKey key,
        Func<CancellationToken, Task<TValue>> factory,
        CancellationToken cancellationToken = default);
}

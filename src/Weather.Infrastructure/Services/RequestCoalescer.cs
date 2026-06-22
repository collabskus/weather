using System.Collections.Concurrent;
using Weather.Core.Abstractions;

namespace Weather.Infrastructure.Services;

/// <summary>
/// Thread-safe, in-process implementation of <see cref="IRequestCoalescer{TKey}"/>.
///
/// <para>
/// <b>How it collapses a stampede.</b> The first caller for a key wins a race
/// to insert a <see cref="Flight"/> into a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// and starts the factory. Concurrent callers for the same key lose the race,
/// find the existing flight, and await its task. The instant the factory
/// completes, the flight is evicted so the NEXT caller (after the cache has
/// presumably been filled by the winner) is free to start a new flight if the
/// cache is cold again.
/// </para>
///
/// <para>
/// <b>Cancellation.</b> A flight is shared by N callers, so one caller's
/// cancellation must not abort the work the other N-1 are waiting on. Each
/// caller therefore awaits with <c>Task.WaitAsync(callerToken)</c>, which
/// detaches only that caller. The underlying factory runs under a flight-owned
/// linked token that is cancelled only when EVERY attached caller has gone
/// away (reference-counted), so an abandoned flight does not keep an upstream
/// call alive needlessly.
/// </para>
///
/// <para>
/// The dictionary key is the coalescing key; the stored value is weakly typed
/// as <see cref="object"/> because a single coalescer instance is generic over
/// the key but each call supplies its own value type. The cast back is always
/// safe: a given key is only ever used with one value type in this codebase
/// (a <c>GridPoint</c> always yields a forecast tuple).
/// </para>
/// </summary>
internal sealed class RequestCoalescer<TKey> : IRequestCoalescer<TKey>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Flight> _flights = new();

    public async Task<TValue> RunAsync<TValue>(
        TKey key,
        Func<CancellationToken, Task<TValue>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        cancellationToken.ThrowIfCancellationRequested();

        // Attach to an existing flight or become the owner of a new one. The
        // loop guards the tiny window where a flight is removed (because it just
        // completed) between our GetOrAdd and our successful attach.
        while (true)
        {
            // Construct a candidate up front and let GetOrAdd decide the winner
            // by reference. Using the value-overload (not the factory-overload)
            // means ownership is unambiguous even under heavy contention: the
            // owner is whoever's candidate is the one actually stored. A losing
            // candidate is simply discarded (and disposed) without ever running
            // the factory, so NWS is never called by a non-owner.
            var candidate = new Flight();
            var flight = _flights.GetOrAdd(key, candidate);
            var isOwner = ReferenceEquals(flight, candidate);

            if (!isOwner)
            {
                // Lost the race: dispose our unused candidate's token source.
                candidate.DisposeUnused();
            }

            // Attach (owner and followers alike). If the flight has already
            // finished and been evicted, start over with a fresh one.
            if (!flight.TryAttach())
            {
                continue;
            }

            // The OWNER kicks off the single factory invocation. It runs
            // detached from any one caller's token (it observes the flight's
            // own token, which is cancelled only when EVERY caller has gone)
            // and arranges its own eviction on completion. Crucially, the owner
            // does NOT await the factory directly — it awaits the shared task
            // below, just like a follower, so the owner's own cancellation
            // detaches only the owner and never aborts work others are awaiting.
            if (isOwner)
            {
                StartFlight(key, flight, factory);
            }

            try
            {
                // Every caller awaits the shared result, but its own token
                // detaches only itself.
                var result = (TValue)await flight.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                return result;
            }
            finally
            {
                flight.Detach();
            }
        }
    }

    /// <summary>
    /// Run the factory exactly once for an owned flight, bind its outcome to the
    /// shared task, and evict the flight when it finishes (success, fault, or
    /// cancellation) so the next caller starts fresh. Runs detached: no single
    /// caller's lifetime governs it.
    /// </summary>
    private void StartFlight<TValue>(
        TKey key, Flight flight, Func<CancellationToken, Task<TValue>> factory)
    {
        Task<TValue> task;
        try
        {
            task = factory(flight.Token);
        }
        catch (Exception ex)
        {
            // Synchronous throw from the factory: fault the shared task so all
            // attached callers observe the failure, then evict.
            flight.Fault(ex);
            EvictAndDispose(key, flight);
            return;
        }

        flight.Bind(task);

        // Detached eviction: when the factory's task settles, remove the flight.
        // We do not await this; the callers await flight.Task instead.
        _ = task.ContinueWith(
            (_, state) =>
            {
                var (self, k, f) = ((RequestCoalescer<TKey>, TKey, Flight))state!;
                self.EvictAndDispose(k, f);
            },
            (this, key, flight),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void EvictAndDispose(TKey key, Flight flight)
    {
        // Remove only if it's still the same flight (a newer one may have taken
        // the slot already). Dispose is deferred until all callers detach via
        // the flight's own ref-count, so we mark it evictable here and let the
        // last Detach dispose it.
        _flights.TryRemove(new KeyValuePair<TKey, Flight>(key, flight));
        flight.MarkEvicted();
    }

    /// <summary>
    /// A single in-flight factory invocation, shared by all concurrent callers
    /// for one key.
    ///
    /// <para>
    /// <b>Lifecycle.</b> Callers attach (ref-count++) and detach (ref-count--).
    /// The flight becomes "evictable" when its factory settles (success, fault
    /// or cancellation) — at which point <see cref="MarkEvicted"/> is called and
    /// it is removed from the dictionary so the next caller starts fresh.
    /// Disposal of the <see cref="CancellationTokenSource"/> is deferred until
    /// BOTH conditions hold: the flight has been evicted AND every attached
    /// caller has detached. That ordering prevents disposing the CTS while a
    /// late follower might still be touching it.
    /// </para>
    ///
    /// <para>
    /// <b>Token semantics.</b> The factory observes <see cref="Token"/>, which is
    /// cancelled only when ALL callers detach before the factory completes — i.e.
    /// the request is genuinely abandoned by everyone. One caller cancelling
    /// detaches only itself (via <c>WaitAsync</c> on the shared task) and never
    /// cancels the shared token while others remain attached.
    /// </para>
    ///
    /// <para>
    /// Implements <see cref="IDisposable"/> so the type satisfies CA1001 (it owns
    /// a disposable <see cref="CancellationTokenSource"/>). In normal operation
    /// the CTS is disposed deterministically through the evict/detach handshake;
    /// <see cref="Dispose"/> is an idempotent alias for that same teardown for
    /// any path that wants to release the candidate explicitly.
    /// </para>
    /// </summary>
    private sealed class Flight : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource<object> _bound =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Attach/detach reference count of live callers.
        private int _refCount;

        // 0 = active, 1 = evicted (factory settled, removed from dictionary).
        private int _evicted;

        // 0 = CTS live, 1 = CTS disposed. Guards against double-dispose and
        // use-after-dispose of the token source.
        private int _ctsDisposed;

        /// <summary>Token the factory observes; cancelled when all callers leave.</summary>
        public CancellationToken Token => _cts.Token;

        /// <summary>
        /// The shared result task. All callers (owner and followers) await this
        /// via <c>WaitAsync</c> with their own token. It is completed from the
        /// factory's task in <see cref="Bind"/> or directly via <see cref="Fault"/>.
        /// </summary>
        public Task<object> Task => _bound.Task;

        /// <summary>
        /// Try to register a caller. Fails once the flight has been evicted, so a
        /// caller that loses the race against eviction loops back and starts a
        /// fresh flight rather than attaching to a dead one.
        /// </summary>
        public bool TryAttach()
        {
            if (Volatile.Read(ref _evicted) != 0)
            {
                return false;
            }

            Interlocked.Increment(ref _refCount);

            // Re-check after incrementing to close the race with MarkEvicted.
            if (Volatile.Read(ref _evicted) != 0)
            {
                // We attached to an evicted flight. Back the count out; if that
                // makes us the last detacher, run the same teardown path.
                DetachCore();
                return false;
            }

            return true;
        }

        public void Detach() => DetachCore();

        private void DetachCore()
        {
            if (Interlocked.Decrement(ref _refCount) != 0)
            {
                return;
            }

            // Last caller gone.
            if (Volatile.Read(ref _evicted) != 0)
            {
                // Factory already settled and flight removed → safe to dispose.
                DisposeCts();
                return;
            }

            // Everyone abandoned the request before the factory finished: signal
            // the factory it may stop. We do NOT dispose the CTS here because the
            // factory's eviction continuation will still run and call MarkEvicted,
            // which disposes once it observes a zero ref-count.
            if (!_cts.IsCancellationRequested && Volatile.Read(ref _ctsDisposed) == 0)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Raced with disposal; nothing to cancel.
                }
            }
        }

        /// <summary>
        /// Marks the flight as removed from the dictionary (its factory has
        /// settled). Disposes the CTS if no callers remain attached.
        /// </summary>
        public void MarkEvicted()
        {
            if (Interlocked.Exchange(ref _evicted, 1) != 0)
            {
                return;
            }

            // If all callers have already detached, dispose now; otherwise the
            // last DetachCore will dispose once it sees _evicted == 1.
            if (Volatile.Read(ref _refCount) == 0)
            {
                DisposeCts();
            }
        }

        public void Fault(Exception ex) => _bound.TrySetException(ex);

        public void Bind<TValue>(Task<TValue> task)
        {
            // Marshal the strongly-typed factory task onto the weakly-typed
            // shared completion source so all callers can await one object.
            _ = task.ContinueWith(
                static (t, state) =>
                {
                    var tcs = (TaskCompletionSource<object>)state!;
                    if (t.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (t.IsFaulted)
                    {
                        tcs.TrySetException(t.Exception!.InnerExceptions);
                    }
                    else
                    {
                        tcs.TrySetResult(t.Result!);
                    }
                },
                _bound,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>Dispose the unused token source of a candidate that lost the GetOrAdd race.</summary>
        public void DisposeUnused() => DisposeCts();

        /// <summary>
        /// Idempotent alias for the CTS teardown. Present to satisfy CA1001;
        /// the normal lifecycle disposes via the evict/detach handshake.
        /// </summary>
        public void Dispose() => DisposeCts();

        private void DisposeCts()
        {
            if (Interlocked.Exchange(ref _ctsDisposed, 1) != 0)
            {
                return;
            }

            _cts.Dispose();
        }
    }
}

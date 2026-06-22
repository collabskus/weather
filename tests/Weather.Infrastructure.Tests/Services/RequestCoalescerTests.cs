using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Infrastructure.Services;

namespace Weather.Infrastructure.Tests.Services;

/// <summary>
/// Focused unit tests for the single-flight coalescer in isolation from the
/// weather service. These pin the contract the stampede fix depends on.
/// </summary>
public sealed class RequestCoalescerTests
{
    private static readonly GridPoint KeyA = new("AKQ", 83, 61);
    private static readonly GridPoint KeyB = new("LWX", 96, 70);

    [Test]
    public async Task ConcurrentCallsForSameKeyRunFactoryOnce()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocations = 0;

        async Task<int> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 42;
        }

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => coalescer.RunAsync(KeyA, Factory))
            .ToArray();

        await Task.Delay(50);
        gate.SetResult();

        var results = await Task.WhenAll(tasks);

        results.ShouldAllBe(r => r == 42);
        invocations.ShouldBe(1);
    }

    [Test]
    public async Task DifferentKeysRunInParallel()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        var invocations = 0;

        Task<int> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult(1);
        }

        await Task.WhenAll(
            coalescer.RunAsync(KeyA, Factory),
            coalescer.RunAsync(KeyB, Factory));

        // Two distinct keys → two independent flights.
        invocations.ShouldBe(2);
    }

    [Test]
    public async Task SequentialCallsEachRunFactory()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        var invocations = 0;

        Task<int> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult(7);
        }

        await coalescer.RunAsync(KeyA, Factory);
        await coalescer.RunAsync(KeyA, Factory);

        // No overlap → flight evicted between calls → factory runs twice.
        invocations.ShouldBe(2);
    }

    [Test]
    public async Task FactoryExceptionPropagatesToAllWaiters()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Factory(CancellationToken _)
        {
            await gate.Task;
            throw new InvalidOperationException("boom");
        }

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => coalescer.RunAsync(KeyA, Factory))
            .ToArray();

        await Task.Delay(50);
        gate.SetResult();

        foreach (var t in tasks)
        {
            await Should.ThrowAsync<InvalidOperationException>(async () => await t);
        }
    }

    [Test]
    public async Task FlightIsEvictedAfterFailureSoNextCallRetries()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        var invocations = 0;

        Task<int> Throwing(CancellationToken _)
        {
            Interlocked.Increment(ref invocations);
            throw new InvalidOperationException();
        }

        await Should.ThrowAsync<InvalidOperationException>(async () => await coalescer.RunAsync(KeyA, Throwing));
        await Should.ThrowAsync<InvalidOperationException>(async () => await coalescer.RunAsync(KeyA, Throwing));

        // A failed flight must not be cached; the second call starts fresh.
        invocations.ShouldBe(2);
    }

    [Test]
    public async Task OneCallerCancellingDoesNotAbortTheSharedResultForOthers()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Factory(CancellationToken _)
        {
            await gate.Task;
            return 99;
        }

        using var cts = new CancellationTokenSource();

        var cancellable = coalescer.RunAsync(KeyA, Factory, cts.Token);
        var survivor = coalescer.RunAsync(KeyA, Factory);

        await Task.Delay(50);

        // Detach the first caller mid-flight.
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await cancellable);

        // The shared flight is still alive for the remaining caller.
        gate.SetResult();
        (await survivor).ShouldBe(99);
    }

    [Test]
    public async Task CancellingBeforeStartThrowsImmediately()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await coalescer.RunAsync(KeyA, _ => Task.FromResult(1), cts.Token));
    }

    [Test]
    public async Task NullKeyThrows()
    {
        var coalescer = new RequestCoalescer<string>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await coalescer.RunAsync(null!, _ => Task.FromResult(1)));
    }

    [Test]
    public async Task NullFactoryThrows()
    {
        var coalescer = new RequestCoalescer<GridPoint>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await coalescer.RunAsync(KeyA, null!));
    }
}

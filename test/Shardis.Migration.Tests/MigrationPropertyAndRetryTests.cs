using Shardis.Logging;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class MigrationPropertyAndRetryTests
{
    private static List<KeyMove<string>> Moves(int n)
    {
        var list = new List<KeyMove<string>>(n);
        for (int i = 0; i < n; i++)
        {
            list.Add(new KeyMove<string>(new ShardKey<string>("k" + i), new("S"), new("T")));
        }
        return list;
    }

    [Fact]
    public async Task ProgressThrottling_MaxOneEventPerInterval_FakeClock()
    {
        // arrange
        var baseTime = DateTimeOffset.UtcNow;
        var current = baseTime + TimeSpan.FromSeconds(1.5); // pre-advance so first emission is immediately eligible
        Func<DateTimeOffset> clock = () => current;
        var mover = new InMemoryDataMover<string>();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        var store = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, SwapBatchSize = 200 };
        var moves = Moves(200);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), baseTime, moves);
        var exec = new ShardMigrationExecutor<string>(mover, verification, swapper, store, metrics, options, new InMemoryShardisLogger(), clock);
        var events = new List<MigrationProgressEvent>();
        var progress = new Progress<MigrationProgressEvent>(e => events.Add(e));

        // act
        var task = exec.ExecuteAsync(plan, progress, CancellationToken.None);
        // allow executor to start and emit first progress (clock already beyond 1s interval)
        for (int i = 0; i < 40 && events.Count == 0; i++) { await Task.Delay(5); }
        events.Count.Should().Be(1); // first emission
        var firstCopied = events[0].Copied;
        // freeze time (no change) to ensure no extra emissions despite many transitions
        await Task.Delay(50);
        events.Count.Should().Be(1);
        // jump forward beyond another interval to allow exactly one more emission (if still running)
        current = current + TimeSpan.FromSeconds(2);
        await task; // finish run
        events.Count.Should().BeLessThanOrEqualTo(2);
        events.Select(e => e.Copied).Should().BeInAscendingOrder();
        events[0].Copied.Should().Be(firstCopied);
    }

    public static IEnumerable<object?[]> IdempotencyMatrix()
    {
        // size, copyConcurrency, verifyConcurrency
        yield return new object?[] { 0, 1, 1 };
        yield return new object?[] { 1, 1, 1 };
        yield return new object?[] { 12, 4, 4 };
        yield return new object?[] { 1000, 16, 8 };
    }

    [Theory]
    [MemberData(nameof(IdempotencyMatrix))]
    public async Task MigrationIdempotency_VariedSizes_Concurrency(int size, int copyConc, int verifyConc)
    {
        // arrange
        var planId = Guid.NewGuid();
        var moves = Moves(size);
        var plan = new MigrationPlan<string>(planId, DateTimeOffset.UtcNow, moves);
        var mover = new InMemoryDataMover<string>();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        var store = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = copyConc, VerifyConcurrency = verifyConc, SwapBatchSize = 256 };
        var exec = new ShardMigrationExecutor<string>(mover, verification, swapper, store, metrics, options, new InMemoryShardisLogger());

        // act
        var summary1 = await exec.ExecuteAsync(plan, null, CancellationToken.None);
        var snap1 = metrics.Snapshot();
        var summary2 = await exec.ExecuteAsync(plan, null, CancellationToken.None);
        var snap2 = metrics.Snapshot();

        // assert
        summary1.Done.Should().Be(size);
        summary2.Done.Should().Be(size);
        if (size > 0)
        {
            snap1.planned.Should().Be(size);
            snap2.planned.Should().Be(size);
            snap2.copied.Should().Be(snap1.copied);
            snap2.verified.Should().Be(snap1.verified);
            snap2.swapped.Should().Be(snap1.swapped);
        }
        snap2.failed.Should().Be(0);
    }

    private static ShardMigrationExecutor<string> Exec(
        IShardDataMover<string> mover,
        IVerificationStrategy<string> verification,
        IShardMapSwapper<string> swapper,
        IShardMigrationCheckpointStore<string> store,
        IShardMigrationMetrics metrics,
        ShardMigrationOptions options)
        => new(mover, verification, swapper, store, metrics, options);

    [Fact]
    public async Task MigrationIdempotency_SamePlanExecutedTwice_ShouldNotDoubleCountMetrics()
    {
        // arrange
        var planId = Guid.NewGuid();
        var moves = Moves(12);
        var plan = new MigrationPlan<string>(planId, DateTimeOffset.UtcNow, moves);
        var mover = new InMemoryDataMover<string>();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        var store = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, SwapBatchSize = 32 };
        var executor = Exec(mover, verification, swapper, store, metrics, options);

        // act (first)
        var summary1 = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap1 = metrics.Snapshot();
        // act (second run, idempotent)
        var summary2 = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap2 = metrics.Snapshot();

        // assert
        summary1.Done.Should().Be(12);
        summary2.Done.Should().Be(12);
        snap1.planned.Should().Be(12);
        snap2.planned.Should().Be(12); // no increment
        snap2.copied.Should().Be(snap1.copied);
        snap2.verified.Should().Be(snap1.verified);
        snap2.swapped.Should().Be(snap1.swapped);
        snap2.failed.Should().Be(0);
        snap2.retries.Should().Be(snap1.retries); // no extra retries
    }

    [Fact]
    public async Task MigrationRetry_MixedTransientAndPermanent_OutcomesCorrect()
    {
        // arrange
        var moves = new List<KeyMove<string>>
        {
            new(new ShardKey<string>("k0"), new("S"), new("T")), // transient copy failures then success
            new(new ShardKey<string>("k1"), new("S"), new("T")), // permanent copy failure
            new(new ShardKey<string>("k2"), new("S"), new("T"))  // clean
        };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);
        var mover = new InMemoryDataMover<string>();
        var attempts = new Dictionary<string, int>();
        mover.CopyFailureInjector = move =>
        {
            var k = move.Key.Value!;
            attempts.TryGetValue(k, out var a);
            a++;
            attempts[k] = a;
            if (k == "k0")
            {
                return a <= 2 ? new InvalidOperationException("transient") : null; // succeed after 2 failures
            }
            if (k == "k1")
            {
                return new InvalidOperationException("permanent");
            }
            return null;
        };
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        var store = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 2, VerifyConcurrency = 2, SwapBatchSize = 10, MaxRetries = 2, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var executor = Exec(mover, verification, swapper, store, metrics, options);

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        // k0 succeeds after retries, k1 fails, k2 succeeds
        summary.Done.Should().Be(2);
        summary.Failed.Should().Be(1);
        attempts["k0"].Should().Be(3); // 2 fails + success
        attempts["k1"].Should().BeGreaterThanOrEqualTo(1); // permanent
        snap.retries.Should().BeGreaterThanOrEqualTo(2);
        snap.failed.Should().Be(1);
        snap.swapped.Should().Be(2);
    }

    private sealed class FlakyVerificationStrategy<TKey>(int failuresBeforeSuccess) : IVerificationStrategy<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly int _failuresBeforeSuccess = failuresBeforeSuccess;
        private int _attempts;

        public Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct)
            => Task.FromResult(Interlocked.Increment(ref _attempts) > _failuresBeforeSuccess);
    }

    [Fact]
    public async Task Execute_VerifyTransientFailures_RetriesThenSucceeds()
    {
        var moves = Moves(5);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);
        var mover = new InMemoryDataMover<string>(); // copy always ok
        var verification = new FlakyVerificationStrategy<string>(failuresBeforeSuccess: 2);
        var swapper = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        var store = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var exec = Exec(mover, verification, swapper, store, metrics, options);

        var summary = await exec.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        summary.Done.Should().Be(5);
        snap.retries.Should().BeGreaterThanOrEqualTo(2);
    }

    private sealed class FlakySwapper<TKey>(IShardMapSwapper<TKey> inner, int failures) : IShardMapSwapper<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly IShardMapSwapper<TKey> _inner = inner;
        private int _failuresLeft = failures;

        public Task SwapAsync(IReadOnlyList<KeyMove<TKey>> batch, CancellationToken ct)
        {
            if (Interlocked.Decrement(ref _failuresLeft) >= 0)
                throw new InvalidOperationException("transient swap");
            return _inner.SwapAsync(batch, ct);
        }
    }

    [Fact]
    public async Task Execute_SwapTransientFailures_RetrySucceeds_NoDoubleCount()
    {
        var moves = Moves(20);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);
        var mover = new InMemoryDataMover<string>();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var inner = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        var swapper = new FlakySwapper<string>(inner, failures: 2);
        var store = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { SwapBatchSize = 10, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var exec = Exec(mover, verification, swapper, store, metrics, options);

        var summary = await exec.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        summary.Done.Should().Be(20);
        snap.swapped.Should().Be(20);
        snap.retries.Should().BeGreaterThanOrEqualTo(2);
        snap.failed.Should().Be(0);
    }
}
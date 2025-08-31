using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class ShardMigrationExecutorAdditionalTests
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

    private static ShardMigrationExecutor<string> MakeExecutor(
        IShardDataMover<string> mover,
        IVerificationStrategy<string> verification,
        IShardMapSwapper<string> swapper,
        IShardMigrationCheckpointStore<string> checkpoint,
        IShardMigrationMetrics metrics,
        ShardMigrationOptions options,
        Func<DateTimeOffset>? clock = null)
        => new(mover, verification, swapper, checkpoint, metrics, options, clock);

    private sealed class RecordingMover : IShardDataMover<string>
    {
        private readonly object _lock = new();
        public readonly List<string> CopyEvents = [];
        public readonly List<string> VerifyEvents = [];
        public readonly List<(string Kind, string Key)> Timeline = [];
        public Func<ShardKey<string>, Exception?>? CopyFail;
        public Func<ShardKey<string>, Exception?>? VerifyFail;
        public Task CopyAsync(KeyMove<string> move, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var ex = CopyFail?.Invoke(move.Key);
            if (ex != null) throw ex;
            var key = move.Key.Value!;
            lock (_lock)
            {
                CopyEvents.Add(key);
                Timeline.Add(("C", key));
            }
            return Task.CompletedTask;
        }
        public Task<bool> VerifyAsync(KeyMove<string> move, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var ex = VerifyFail?.Invoke(move.Key);
            if (ex != null) throw ex;
            var key = move.Key.Value!;
            lock (_lock)
            {
                VerifyEvents.Add(key);
                Timeline.Add(("V", key));
            }
            return Task.FromResult(true);
        }
    }

    private sealed class TransientSwapSwapper(int failures, InMemoryShardMapStore<string> store) : IShardMapSwapper<string>
    {
        private readonly int _failures = failures;
        private readonly HashSet<string> _applied = [];
        private readonly InMemoryShardMapStore<string> _store = store;

        public int Attempts { get; private set; }
        public IReadOnlyCollection<string> Applied => _applied;
        public Task SwapAsync(IReadOnlyList<KeyMove<string>> verifiedBatch, CancellationToken ct)
        {
            Attempts++;
            if (Attempts <= _failures)
            {
                throw new InvalidOperationException("transient swap fail");
            }
            foreach (var m in verifiedBatch)
            {
                _store.AssignShardToKey(m.Key, m.Target);
                _applied.Add(m.Key.Value!);
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Execute_NonInterleaved_CopiesThenVerifiesThenSwaps()
    {
        // arrange
        var mover = new RecordingMover();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { InterleaveCopyAndVerify = false, CopyConcurrency = 1, VerifyConcurrency = 1, SwapBatchSize = 100 };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(8));
        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options);

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        summary.Done.Should().Be(8);
        mover.CopyEvents.Count.Should().Be(8);
        mover.VerifyEvents.Count.Should().Be(8);
        // ensure first verify appears only after all copies
        mover.Timeline.Count.Should().Be(16);
        mover.Timeline.Take(8).All(t => t.Kind == "C").Should().BeTrue();
        mover.Timeline.Skip(8).All(t => t.Kind == "V").Should().BeTrue();
    }

    [Fact]
    public async Task Execute_VerifyTransientFailures_RetriesThenSucceeds()
    {
        // arrange
        var mover = new RecordingMover();
        int failCount = 0;
        mover.VerifyFail = _ => failCount++ < 2 ? new InvalidOperationException("vfail") : null;
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 1, VerifyConcurrency = 1, MaxRetries = 5, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(1));
        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options);

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(1);
        failCount.Should().Be(3); // two failures + success attempt
        snap.retries.Should().BeGreaterThanOrEqualTo(2);
        snap.verified.Should().Be(1);
    }

    [Fact]
    public async Task Execute_CopyTransientFailures_RetriesThenSucceeds()
    {
        // arrange
        var mover = new RecordingMover();
        int copyAttempts = 0;
        mover.CopyFail = _ => copyAttempts++ < 3 ? new InvalidOperationException("copy-transient") : null; // 3 failures then success
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 1, VerifyConcurrency = 1, MaxRetries = 5, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(1));
        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options);

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(1);
        copyAttempts.Should().Be(4); // 3 failures + success
        snap.retries.Should().BeGreaterThanOrEqualTo(3);
        snap.copied.Should().Be(1);
        snap.swapped.Should().Be(1);
    }

    [Fact]
    public async Task Execute_SwapTransientFailures_RetryBatchSucceeds()
    {
        // arrange
        var mover = new RecordingMover();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var mapStore = new InMemoryShardMapStore<string>();
        var swapper = new TransientSwapSwapper(failures: 2, mapStore);
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 2, VerifyConcurrency = 2, MaxRetries = 5, RetryBaseDelay = TimeSpan.FromMilliseconds(1), SwapBatchSize = 50 };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(5));
        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options);

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(5);
        swapper.Applied.Count.Should().Be(5);
        swapper.Attempts.Should().BeGreaterThanOrEqualTo(3); // 2 fails + success
        snap.retries.Should().BeGreaterThanOrEqualTo(2);
        snap.swapped.Should().Be(5);
    }

    [Fact]
    public async Task Execute_ProgressThrottled_MaxOneEventPerInterval()
    {
        // arrange
        var fixedNow = DateTimeOffset.UtcNow;
        Func<DateTimeOffset> clock = () => fixedNow; // constant time -> only first progress event allowed
        var mover = new RecordingMover();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, SwapBatchSize = 100 };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(20));
        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options, clock);
        var events = new List<MigrationProgressEvent>();
        var progress = new Progress<MigrationProgressEvent>(e => events.Add(e));

        // act
        var summary = await executor.ExecuteAsync(plan, progress, CancellationToken.None);

        // assert
        summary.Done.Should().Be(20);
        events.Count.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Executor_Respects_CopyAndVerifyConcurrencyLimits()
    {
        // arrange
        var mover = new RecordingMover();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 3, VerifyConcurrency = 2, SwapBatchSize = 100 };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(40));
        int observedCopyMax = 0, observedVerifyMax = 0;
        // wrap metrics via polling progress
        var events = new List<MigrationProgressEvent>();
        var progress = new Progress<MigrationProgressEvent>(e =>
        {
            events.Add(e);
            if (e.ActiveCopy > observedCopyMax) observedCopyMax = e.ActiveCopy;
            if (e.ActiveVerify > observedVerifyMax) observedVerifyMax = e.ActiveVerify;
        });

        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options);

        // act
        await executor.ExecuteAsync(plan, progress, CancellationToken.None);

        // assert
        observedCopyMax.Should().BeLessThanOrEqualTo(options.CopyConcurrency);
        observedVerifyMax.Should().BeLessThanOrEqualTo(options.VerifyConcurrency);
    }

    [Fact]
    public async Task Executor_CheckpointFlushes_ByTransitionCount_And_ByInterval()
    {
        // arrange
        var now = DateTimeOffset.UtcNow;
        var mutableNow = now;
        Func<DateTimeOffset> clock = () => mutableNow;
        var mover = new RecordingMover();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var checkpointStore = new CountingCheckpointStore<string>();
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 2, VerifyConcurrency = 2, SwapBatchSize = 100, CheckpointFlushEveryTransitions = 3, CheckpointFlushInterval = TimeSpan.FromSeconds(5) };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), now, Moves(7));
        var executor = MakeExecutor(mover, verification, swapper, checkpointStore, metrics, options, clock);

        // act (first part: trigger transition-based flushes)
        await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var countAfterRun = checkpointStore.PersistCount;
        countAfterRun.Should().BeGreaterThanOrEqualTo(3); // several flushes (transition-based + final)

        // second run with time-based flush: create new plan id
        var plan2 = new MigrationPlan<string>(Guid.NewGuid(), now, Moves(2));
        mutableNow = now; // reset clock
        var executor2 = MakeExecutor(mover, verification, swapper, checkpointStore, metrics, options, clock);
        // advance time once mid-run to force interval flush
        var task = executor2.ExecuteAsync(plan2, null, CancellationToken.None);
        mutableNow = now + TimeSpan.FromSeconds(6); // beyond interval
        await task;
        checkpointStore.PersistCount.Should().BeGreaterThan(countAfterRun);
    }

    [Fact]
    public async Task Checkpoint_Updates_LastProcessedIndex_OnTerminalTransitions()
    {
        // arrange
        var mover = new RecordingMover();
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var store = new InspectableCheckpointStore<string>();
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 1, VerifyConcurrency = 1, SwapBatchSize = 10, CheckpointFlushEveryTransitions = 1, CheckpointFlushInterval = TimeSpan.FromMilliseconds(1) };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(5));
        var executor = MakeExecutor(mover, verification, swapper, store, metrics, options);

        // act
        await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        // last processed index should be final move index (4)
        store.LastPersisted?.LastProcessedIndex.Should().Be(4);
        // monotonic: sequence of persisted indexes should be non-decreasing
        store.PersistedIndexes.Zip(store.PersistedIndexes.Skip(1), (a, b) => a <= b).All(x => x).Should().BeTrue();
    }

    [Fact]
    public async Task Metrics_Completeness_FailureAndRetryCounters()
    {
        // arrange: induce one verify failure (permanent) and one retry success
        var mover = new RecordingMover();
        int verifyAttempts = 0;
        mover.VerifyFail = key =>
        {
            verifyAttempts++;
            if (key.Value == "k0")
            {
                return new InvalidOperationException("permanent");
            }
            if (key.Value == "k1" && verifyAttempts < 3)
            {
                return new InvalidOperationException("transient");
            }
            return null;
        };
        var verification = new FullEqualityVerificationStrategy<string>(mover);
        var swapper = new InMemoryMapSwapper<string>(new InMemoryShardMapStore<string>());
        var checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { CopyConcurrency = 1, VerifyConcurrency = 1, MaxRetries = 5, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Moves(3));
        var executor = MakeExecutor(mover, verification, swapper, checkpoint, metrics, options);

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(2); // k1 & k2 succeed; k0 fails
        summary.Failed.Should().Be(1);
        snap.failed.Should().Be(1);
        snap.verified.Should().BeGreaterThanOrEqualTo(2);
        snap.swapped.Should().BeGreaterThanOrEqualTo(2);
        snap.retries.Should().BeGreaterThanOrEqualTo(2); // k1 transient retries
    }
}
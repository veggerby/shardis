using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class ShardMigrationExecutorTests
{
    private static List<KeyMove<string>> MakeMoves(int count, string source = "s1", string target = "s2")
    {
        var list = new List<KeyMove<string>>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new KeyMove<string>(new ShardKey<string>("k" + i), new(source), new(target)));
        }
        return list;
    }

    private static ShardMigrationExecutor<string> MakeExecutor(
        IShardDataMover<string>? mover = null,
        IVerificationStrategy<string>? verification = null,
        IShardMapSwapper<string>? swapper = null,
        IShardMigrationCheckpointStore<string>? checkpoint = null,
        IShardMigrationMetrics? metrics = null,
        ShardMigrationOptions? options = null)
    {
        mover ??= new InMemoryDataMover<string>();
        verification ??= new FullEqualityVerificationStrategy<string>(mover);
        swapper ??= new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>());
        checkpoint ??= new InMemoryCheckpointStore<string>();
        metrics ??= new SimpleShardMigrationMetrics();
        options ??= new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, SwapBatchSize = 32, MaxRetries = 5, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        return new ShardMigrationExecutor<string>(mover, verification, swapper, checkpoint, metrics, options);
    }

    [Fact]
    public async Task Execute_All_Succeeds_Should_Produce_Done_Summary()
    {
        // arrange
        var moves = MakeMoves(10);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);
        var metrics = new SimpleShardMigrationMetrics();
        var executor = MakeExecutor(metrics: metrics);

        // act
        var summary = await executor.ExecuteAsync(plan, progress: null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Planned.Should().Be(10);
        summary.Done.Should().Be(10);
        summary.Failed.Should().Be(0);
        snap.planned.Should().Be(10);
        snap.copied.Should().Be(10);
        snap.verified.Should().Be(10);
        snap.swapped.Should().Be(10);
        snap.failed.Should().Be(0);
    }

    [Fact]
    public async Task Resume_From_Checkpoint_Should_Not_Recount_Planned()
    {
        // arrange
        var moves = MakeMoves(6);
        var planId = Guid.NewGuid();
        var plan = new MigrationPlan<string>(planId, DateTimeOffset.UtcNow, moves);
        var checkpointStore = new InMemoryCheckpointStore<string>();
        // Simulate 3 already verified
        var states = moves.Take(3).ToDictionary(m => m.Key, _ => KeyMoveState.Verified);
        foreach (var m in moves.Skip(3))
        {
            states[m.Key] = KeyMoveState.Planned;
        }
        await checkpointStore.PersistAsync(new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow, states, lastProcessedIndex: 2), CancellationToken.None);
        var metrics = new SimpleShardMigrationMetrics();
        var executor = MakeExecutor(checkpoint: checkpointStore, metrics: metrics);

        // act
        var summary = await executor.ExecuteAsync(plan, progress: null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(6);
        snap.planned.Should().Be(0); // planned only increments on first ever run (checkpoint existed)
        snap.swapped.Should().Be(6);
    }

    [Fact]
    public async Task Copy_Retry_Eventually_Succeeds_Should_Increment_Retries()
    {
        // arrange
        var mover = new InMemoryDataMover<string>();
        var attempts = new Dictionary<string, int>();
        mover.CopyFailureInjector = move =>
        {
            var key = move.Key.Value!;
            attempts.TryGetValue(key, out var a);
            a++;
            attempts[key] = a;
            return a <= 2 ? new InvalidOperationException("transient") : null; // fail twice then succeed
        };
        var metrics = new SimpleShardMigrationMetrics();
        var options = new ShardMigrationOptions { MaxRetries = 5, RetryBaseDelay = TimeSpan.FromMilliseconds(1), CopyConcurrency = 1, VerifyConcurrency = 1, SwapBatchSize = 10 };
        var executor = MakeExecutor(mover: mover, metrics: metrics, options: options);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, MakeMoves(1));

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(1);
        snap.retries.Should().BeGreaterThanOrEqualTo(2);
        attempts["k0"].Should().Be(3); // 2 failures + 1 success
    }

    [Fact]
    public async Task Verification_Failure_Without_Force_Should_Fail_Key()
    {
        // arrange
        var mover = new InMemoryDataMover<string> { VerificationMismatchInjector = _ => true };
        var options = new ShardMigrationOptions { ForceSwapOnVerificationFailure = false, RetryBaseDelay = TimeSpan.FromMilliseconds(1), MaxRetries = 1, CopyConcurrency = 1, VerifyConcurrency = 1 };
        var metrics = new SimpleShardMigrationMetrics();
        var executor = MakeExecutor(mover: mover, metrics: metrics, options: options);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, MakeMoves(1));

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(0);
        summary.Failed.Should().Be(1);
        snap.failed.Should().Be(1);
    }

    [Fact]
    public async Task Verification_Failure_With_Force_Should_Swap_Key()
    {
        // arrange
        var mover = new InMemoryDataMover<string> { VerificationMismatchInjector = _ => true };
        var options = new ShardMigrationOptions { ForceSwapOnVerificationFailure = true, RetryBaseDelay = TimeSpan.FromMilliseconds(1), MaxRetries = 0, CopyConcurrency = 1, VerifyConcurrency = 1 };
        var metrics = new SimpleShardMigrationMetrics();
        var executor = MakeExecutor(mover: mover, metrics: metrics, options: options);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, MakeMoves(1));

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Done.Should().Be(1);
        summary.Failed.Should().Be(0);
        snap.swapped.Should().Be(1);
    }

    [Fact]
    public async Task Partial_Swap_Failure_Should_Fail_First_Key_And_Succeed_Rest()
    {
        // arrange
        var mover = new InMemoryDataMover<string>();
        var swapper = new InMemoryMapSwapper<string>(new Persistence.InMemoryShardMapStore<string>()) { SimulatePartialFailure = true };
        var options = new ShardMigrationOptions { SwapBatchSize = 10, MaxRetries = 1, RetryBaseDelay = TimeSpan.FromMilliseconds(1), CopyConcurrency = 4, VerifyConcurrency = 4 };
        var metrics = new SimpleShardMigrationMetrics();
        var executor = MakeExecutor(mover: mover, swapper: swapper, metrics: metrics, options: options);
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, MakeMoves(4));

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var snap = metrics.Snapshot();

        // assert
        summary.Planned.Should().Be(4);
        summary.Done.Should().Be(3);
        summary.Failed.Should().Be(1);
        snap.failed.Should().Be(1);
        snap.swapped.Should().Be(3);
    }

    [Fact]
    public async Task Cancellation_Should_Stop_And_Persist_Partial_Progress()
    {
        // arrange
        var mover = new InMemoryDataMover<string>();
        mover.CopyFailureInjector = _ => { Thread.Sleep(5); return null; }; // slow copies slightly
        var checkpointStore = new InMemoryCheckpointStore<string>();
        var options = new ShardMigrationOptions { CopyConcurrency = 1, VerifyConcurrency = 1, SwapBatchSize = 100, RetryBaseDelay = TimeSpan.FromMilliseconds(1) };
        var executor = MakeExecutor(mover: mover, checkpoint: checkpointStore, options: options);
        var planId = Guid.NewGuid();
        var plan = new MigrationPlan<string>(planId, DateTimeOffset.UtcNow, MakeMoves(50));
        using var cts = new CancellationTokenSource();
        var progressEvents = new List<MigrationProgressEvent>();
        var progress = new Progress<MigrationProgressEvent>(e =>
        {
            progressEvents.Add(e);
            if (progressEvents.Count == 1)
            {
                cts.Cancel();
            }
        });

        // act
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(plan, progress, cts.Token));
        var cp = await checkpointStore.LoadAsync(planId, CancellationToken.None);

        // assert
        cp.Should().NotBeNull();
        cp!.States.Values.Count(s => s != KeyMoveState.Planned).Should().BeGreaterThanOrEqualTo(1);
        progressEvents.Should().NotBeEmpty();
    }
}
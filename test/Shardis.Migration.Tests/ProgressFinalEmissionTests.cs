using Shardis.Logging;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class ProgressFinalEmissionTests
{
    private sealed class NoOpMover : IShardDataMover<string>
    {
        public Task CopyAsync(KeyMove<string> move, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> VerifyAsync(KeyMove<string> move, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class NoOpVerification : IVerificationStrategy<string>
    {
        public Task<bool> VerifyAsync(KeyMove<string> move, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class NoOpSwapper : IShardMapSwapper<string>
    {
        public Task SwapAsync(IReadOnlyList<KeyMove<string>> verifiedMoves, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class InMemoryCheckpointStore : IShardMigrationCheckpointStore<string>
    {
        public Task<MigrationCheckpoint<string>?> LoadAsync(Guid planId, CancellationToken ct) => Task.FromResult<MigrationCheckpoint<string>?>(null);
        public Task PersistAsync(MigrationCheckpoint<string> checkpoint, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class NoOpMetrics : IShardMigrationMetrics
    {
        public void IncPlanned(long delta = 1) { }
        public void IncCopied(long delta = 1) { }
        public void IncVerified(long delta = 1) { }
        public void IncSwapped(long delta = 1) { }
        public void IncFailed(long delta = 1) { }
        public void IncRetries(long delta = 1) { }
        public void SetActiveCopy(int value) { }
        public void SetActiveVerify(int value) { }
        public void ObserveCopyDuration(double ms) { }
        public void ObserveVerifyDuration(double ms) { }
        public void ObserveSwapBatchDuration(double ms) { }
        public void ObserveTotalElapsed(double ms) { }
    }

    [Fact]
    public async Task FinalProgressEventReflectsCompletionEvenIfUnderThrottle()
    {
        // arrange
        var moves = Enumerable.Range(0, 5)
            .Select(i => new KeyMove<string>(new ShardKey<string>($"k{i}"), new ShardId("s1"), new ShardId("s2")))
            .ToList();
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);
        var events = new List<MigrationProgressEvent>();
        var progress = new Progress<MigrationProgressEvent>(e => events.Add(e));
        var executor = new ShardMigrationExecutor<string>(
            new NoOpMover(),
            new NoOpVerification(),
            new NoOpSwapper(),
            new InMemoryCheckpointStore(),
            new NoOpMetrics(),
            new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, SwapBatchSize = 10 },
            new InMemoryShardisLogger());

        // act
        await executor.ExecuteAsync(plan, progress, CancellationToken.None);

        // assert
        Assert.NotEmpty(events);
        var final = events[^1];
        Assert.Equal(5, final.Copied);
        Assert.Equal(5, final.Verified);
        Assert.Equal(5, final.Swapped);
    }
}
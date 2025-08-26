namespace Shardis.Migration.Execution;

using System.Collections.Concurrent;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

/// <summary>Summary of a migration execution.</summary>
/// <param name="PlanId">Plan identifier.</param>
/// <param name="Planned">Total planned key moves.</param>
/// <param name="Done">Number of successfully migrated keys.</param>
/// <param name="Failed">Number of permanently failed keys.</param>
/// <param name="Elapsed">Total elapsed wall-clock time.</param>
public sealed record MigrationSummary(Guid PlanId, int Planned, int Done, int Failed, TimeSpan Elapsed);

/// <summary>
/// Orchestrates execution of a migration plan: copy, verify, swap, checkpoint, metrics &amp; progress.
/// </summary>
internal sealed class ShardMigrationExecutor<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardDataMover<TKey> _mover;
    private readonly IVerificationStrategy<TKey> _verification;
    private readonly IShardMapSwapper<TKey> _swapper;
    private readonly IShardMigrationCheckpointStore<TKey> _checkpointStore;
    private readonly IShardMigrationMetrics _metrics;
    private readonly ShardMigrationOptions _options;

    private const int CheckpointVersion = 1;

    public ShardMigrationExecutor(
        IShardDataMover<TKey> mover,
        IVerificationStrategy<TKey> verification,
        IShardMapSwapper<TKey> swapper,
        IShardMigrationCheckpointStore<TKey> checkpointStore,
        IShardMigrationMetrics metrics,
        ShardMigrationOptions options)
    {
        _mover = mover;
        _verification = verification;
        _swapper = swapper;
        _checkpointStore = checkpointStore;
        _metrics = metrics;
        _options = options;
    }

    /// <summary>
    /// Executes the provided migration plan.
    /// </summary>
    public async Task<MigrationSummary> ExecuteAsync(
        MigrationPlan<TKey> plan,
        IProgress<MigrationProgressEvent>? progress,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var checkpoint = await _checkpointStore.LoadAsync(plan.PlanId, ct).ConfigureAwait(false);

        var states = checkpoint?.States is { Count: > 0 }
            ? new Dictionary<ShardKey<TKey>, KeyMoveState>(checkpoint.States)
            : plan.Moves.ToDictionary(m => m.Key, _ => KeyMoveState.Planned);

        // metrics for planned only the first time
        if (checkpoint is null)
        {
            _metrics.IncPlanned(states.Count);
        }

        var lastProcessedIndex = checkpoint?.LastProcessedIndex ?? -1;
        var total = plan.Moves.Count;

        var pendingSwap = new List<KeyMove<TKey>>();
        var transitionSinceFlush = 0;
        var lastFlushAt = DateTimeOffset.UtcNow;
        var lastProgressAt = DateTimeOffset.MinValue;

        int activeCopy = 0, activeVerify = 0;

        var copySemaphore = new SemaphoreSlim(_options.CopyConcurrency);
        var verifySemaphore = new SemaphoreSlim(_options.VerifyConcurrency);
        var inFlightCopies = new List<Task>();

        var failedKeys = new ConcurrentDictionary<ShardKey<TKey>, byte>();

        // Helper local functions
        async Task ExecuteWithRetry(Func<Task> action, string phase, KeyMove<TKey> move)
        {
            int attempt = 0;
            for (; ; )
            {
                try
                {
                    await action().ConfigureAwait(false);
                    return;
                }
                catch (Exception) when (attempt < _options.MaxRetries && !ct.IsCancellationRequested)
                {
                    attempt++;
                    _metrics.IncRetries();
                    var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    if (delay > TimeSpan.FromSeconds(10))
                    {
                        delay = TimeSpan.FromSeconds(10);
                    }
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // mark failed
                    failedKeys.TryAdd(move.Key, 0);
                    states[move.Key] = KeyMoveState.Failed;
                    _metrics.IncFailed();
                    transitionSinceFlush++;
                    return;
                }
            }
        }

        void EmitProgressIfNeeded()
        {
            if (progress is null)
            {
                return;
            }
            var now = DateTimeOffset.UtcNow;
            if (now - lastProgressAt < TimeSpan.FromSeconds(1))
            {
                return;
            }
            lastProgressAt = now;
            var copied = states.Values.Count(s => s is KeyMoveState.Copied or KeyMoveState.Verifying or KeyMoveState.Verified or KeyMoveState.Swapping or KeyMoveState.Done);
            var verified = states.Values.Count(s => s is KeyMoveState.Verified or KeyMoveState.Swapping or KeyMoveState.Done);
            var swapped = states.Values.Count(s => s is KeyMoveState.Done);
            var failed = states.Values.Count(s => s is KeyMoveState.Failed);
            progress.Report(new MigrationProgressEvent(plan.PlanId, total, copied, verified, swapped, failed, activeCopy, activeVerify, now));
        }

        async Task PersistCheckpointIfNeeded(bool force, CancellationToken token)
        {
            if (!force)
            {
                if (transitionSinceFlush < _options.CheckpointFlushEveryTransitions && DateTimeOffset.UtcNow - lastFlushAt < _options.CheckpointFlushInterval)
                {
                    return;
                }
            }
            var cp = new MigrationCheckpoint<TKey>(plan.PlanId, CheckpointVersion, DateTimeOffset.UtcNow, states, lastProcessedIndex);
            await _checkpointStore.PersistAsync(cp, token).ConfigureAwait(false);
            transitionSinceFlush = 0;
            lastFlushAt = DateTimeOffset.UtcNow;
        }

        // Process moves sequentially for deterministic order while leveraging concurrency for copy/verify operations.
        for (int i = 0; i < plan.Moves.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var move = plan.Moves[i];

            if (states[move.Key] >= KeyMoveState.Copied)
            {
                // Already processed copy stage from previous run.
                if (states[move.Key] == KeyMoveState.Verified)
                {
                    pendingSwap.Add(move);
                }
                continue;
            }

            await copySemaphore.WaitAsync(ct).ConfigureAwait(false);
            Interlocked.Increment(ref activeCopy);
            var copyTask = Task.Run(async () =>
            {
                try
                {
                    states[move.Key] = KeyMoveState.Copying;
                    await ExecuteWithRetry(() => _mover.CopyAsync(move, ct), "copy", move).ConfigureAwait(false);
                    if (states[move.Key] != KeyMoveState.Failed)
                    {
                        states[move.Key] = KeyMoveState.Copied;
                        _metrics.IncCopied();
                        transitionSinceFlush++;
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeCopy);
                    copySemaphore.Release();
                }

                if (_options.InterleaveCopyAndVerify && states[move.Key] == KeyMoveState.Copied)
                {
                    await StartVerifyAsync(move, ct).ConfigureAwait(false);
                }
            }, ct);
            inFlightCopies.Add(copyTask);

            // Maintain concurrency window
            if (inFlightCopies.Count >= _options.CopyConcurrency)
            {
                var finished = await Task.WhenAny(inFlightCopies).ConfigureAwait(false);
                inFlightCopies.Remove(finished);
            }

            EmitProgressIfNeeded();
            await PersistCheckpointIfNeeded(false, ct).ConfigureAwait(false);
        }

        // Wait for remaining copy tasks
        await Task.WhenAll(inFlightCopies).ConfigureAwait(false);

        // If not interleaving, perform verification phase now.
        if (!_options.InterleaveCopyAndVerify)
        {
            foreach (var move in plan.Moves)
            {
                if (states[move.Key] == KeyMoveState.Copied)
                {
                    await StartVerifyAsync(move, ct).ConfigureAwait(false);
                }
            }
        }

        // Ensure all verifications completed (they run inline in this implementation)
        EmitProgressIfNeeded();

        // Perform swap batches.
        foreach (var move in plan.Moves)
        {
            if (states[move.Key] == KeyMoveState.Verified)
            {
                pendingSwap.Add(move);
                if (pendingSwap.Count >= _options.SwapBatchSize)
                {
                    await SwapBatchAsync(pendingSwap, ct).ConfigureAwait(false);
                    pendingSwap.Clear();
                }
            }
        }
        if (pendingSwap.Count > 0)
        {
            await SwapBatchAsync(pendingSwap, ct).ConfigureAwait(false);
            pendingSwap.Clear();
        }

        // Final checkpoint & progress.
        await PersistCheckpointIfNeeded(true, ct).ConfigureAwait(false);
        EmitProgressIfNeeded();

        var done = states.Values.Count(s => s == KeyMoveState.Done);
        var failed = states.Values.Count(s => s == KeyMoveState.Failed);
        return new MigrationSummary(plan.PlanId, total, done, failed, DateTimeOffset.UtcNow - started);

        async Task StartVerifyAsync(KeyMove<TKey> move, CancellationToken token)
        {
            await verifySemaphore.WaitAsync(token).ConfigureAwait(false);
            Interlocked.Increment(ref activeVerify);
            try
            {
                states[move.Key] = KeyMoveState.Verifying;
                await ExecuteWithRetry(async () =>
                {
                    var ok = await _verification.VerifyAsync(move, token).ConfigureAwait(false);
                    if (!ok && !_options.ForceSwapOnVerificationFailure)
                    {
                        throw new InvalidOperationException("Verification failed");
                    }
                }, "verify", move).ConfigureAwait(false);
                if (states[move.Key] != KeyMoveState.Failed)
                {
                    states[move.Key] = KeyMoveState.Verified;
                    _metrics.IncVerified();
                    transitionSinceFlush++;
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeVerify);
                verifySemaphore.Release();
            }
        }

        async Task SwapBatchAsync(List<KeyMove<TKey>> batch, CancellationToken token)
        {
            if (batch.Count == 0)
            {
                return;
            }
            // Transition to Swapping
            foreach (var m in batch)
            {
                if (states[m.Key] == KeyMoveState.Verified)
                {
                    states[m.Key] = KeyMoveState.Swapping;
                }
            }

            await ExecuteWithRetry(() => _swapper.SwapAsync(batch, token), "swap", batch[0]).ConfigureAwait(false);

            // Complete successful swaps
            foreach (var m in batch)
            {
                if (states[m.Key] == KeyMoveState.Swapping)
                {
                    states[m.Key] = KeyMoveState.Done;
                    _metrics.IncSwapped();
                    transitionSinceFlush++;
                }
            }
        }
    }
}
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
/// Orchestrates execution of a migration plan: copy, optional interleaved verify, and batched swap with retry and checkpointing.
/// Public for benchmarking and advanced scenarios; general consumption may wrap this in higher-level services later.
/// </summary>
/// <remarks>
/// See ADR 0002 (Key Migration Execution Model) for design rationale, invariants and extension points.
/// </remarks>
public sealed class ShardMigrationExecutor<TKey>(
    IShardDataMover<TKey> mover,
    IVerificationStrategy<TKey> verification,
    IShardMapSwapper<TKey> swapper,
    IShardMigrationCheckpointStore<TKey> checkpointStore,
    IShardMigrationMetrics metrics,
    ShardMigrationOptions options,
    Func<DateTimeOffset>? timeProvider = null)
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardDataMover<TKey> _mover = mover;
    private readonly IVerificationStrategy<TKey> _verification = verification;
    private readonly IShardMapSwapper<TKey> _swapper = swapper;
    private readonly IShardMigrationCheckpointStore<TKey> _checkpointStore = checkpointStore;
    private readonly IShardMigrationMetrics _metrics = metrics;
    private readonly ShardMigrationOptions _options = options;
    private readonly Func<DateTimeOffset> _now = timeProvider ?? (() => DateTimeOffset.UtcNow);

    private const int CheckpointVersion = 1;

    /// <summary>
    /// Executes the specified migration plan to completion (or until cancellation), applying copy, verify and swap phases.
    /// </summary>
    /// <param name="plan">The migration plan to execute.</param>
    /// <param name="progress">Optional progress reporter throttled to at most one event per second.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary with counts of completed and failed moves.</returns>
    public async Task<MigrationSummary> ExecuteAsync(
        MigrationPlan<TKey> plan,
        IProgress<MigrationProgressEvent>? progress,
        CancellationToken ct)
    {
        var started = _now();
        var checkpoint = await _checkpointStore.LoadAsync(plan.PlanId, ct).ConfigureAwait(false);

        var states = checkpoint?.States is { Count: > 0 }
            ? new Dictionary<ShardKey<TKey>, KeyMoveState>(checkpoint.States)
            : plan.Moves.ToDictionary(m => m.Key, _ => KeyMoveState.Planned);

        if (checkpoint is null)
        {
            _metrics.IncPlanned(states.Count);
        }

        var lastProcessedIndex = checkpoint?.LastProcessedIndex ?? -1;
        var total = plan.Moves.Count;

        var indexByKey = new Dictionary<ShardKey<TKey>, int>(plan.Moves.Count);
        for (int ii = 0; ii < plan.Moves.Count; ii++)
        {
            indexByKey[plan.Moves[ii].Key] = ii;
        }

        var pendingSwap = new List<KeyMove<TKey>>();
        var transitionSinceFlush = 0;
        var lastFlushAt = _now();
        var lastProgressAt = DateTimeOffset.MinValue;

        int activeCopy = 0, activeVerify = 0;
        var copySemaphore = new SemaphoreSlim(_options.CopyConcurrency);
        var verifySemaphore = new SemaphoreSlim(_options.VerifyConcurrency);
        var inFlightCopies = new List<Task>();
        var failedKeys = new ConcurrentDictionary<ShardKey<TKey>, byte>();

        void UpdateLastProcessed(ShardKey<TKey> key)
        {
            if (indexByKey.TryGetValue(key, out var idx))
            {
                while (true)
                {
                    var snapshot = lastProcessedIndex;
                    if (idx <= snapshot)
                    {
                        return;
                    }
                    var updated = Interlocked.CompareExchange(ref lastProcessedIndex, idx, snapshot);
                    if (updated == snapshot)
                    {
                        return;
                    }
                }
            }
        }

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
                    failedKeys.TryAdd(move.Key, 0);
                    states[move.Key] = KeyMoveState.Failed;
                    _metrics.IncFailed();
                    transitionSinceFlush++;
                    UpdateLastProcessed(move.Key);
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
            var now = _now();
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
                if (transitionSinceFlush < _options.CheckpointFlushEveryTransitions && _now() - lastFlushAt < _options.CheckpointFlushInterval)
                {
                    return;
                }
            }
            var cp = new MigrationCheckpoint<TKey>(plan.PlanId, CheckpointVersion, _now(), states, lastProcessedIndex);
            await _checkpointStore.PersistAsync(cp, token).ConfigureAwait(false);
            transitionSinceFlush = 0;
            lastFlushAt = _now();
        }

        bool completed = false;
        try
        {
            for (int i = 0; i < plan.Moves.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var move = plan.Moves[i];
                if (states[move.Key] >= KeyMoveState.Copied)
                {
                    if (states[move.Key] == KeyMoveState.Verified)
                    {
                        pendingSwap.Add(move);
                    }
                    continue;
                }

                await copySemaphore.WaitAsync(ct).ConfigureAwait(false);
                Interlocked.Increment(ref activeCopy);
                _metrics.SetActiveCopy(Volatile.Read(ref activeCopy));
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
                        _metrics.SetActiveCopy(Volatile.Read(ref activeCopy));
                        copySemaphore.Release();
                    }

                    if (_options.InterleaveCopyAndVerify && states[move.Key] == KeyMoveState.Copied)
                    {
                        await StartVerifyAsync(move, ct).ConfigureAwait(false);
                    }
                }, ct);
                inFlightCopies.Add(copyTask);

                if (inFlightCopies.Count >= _options.CopyConcurrency)
                {
                    var finished = await Task.WhenAny(inFlightCopies).ConfigureAwait(false);
                    inFlightCopies.Remove(finished);
                }

                EmitProgressIfNeeded();
                await PersistCheckpointIfNeeded(false, ct).ConfigureAwait(false);
            }

            await Task.WhenAll(inFlightCopies).ConfigureAwait(false);

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

            EmitProgressIfNeeded();

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

            await PersistCheckpointIfNeeded(true, ct).ConfigureAwait(false);
            EmitProgressIfNeeded();

            var done = states.Values.Count(s => s == KeyMoveState.Done);
            var failed = states.Values.Count(s => s == KeyMoveState.Failed);
            completed = true;
            return new MigrationSummary(plan.PlanId, total, done, failed, _now() - started);
        }
        finally
        {
            if (!completed)
            {
                try
                {
                    var cp = new MigrationCheckpoint<TKey>(plan.PlanId, CheckpointVersion, _now(), states, lastProcessedIndex);
                    await _checkpointStore.PersistAsync(cp, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        async Task StartVerifyAsync(KeyMove<TKey> move, CancellationToken token)
        {
            await verifySemaphore.WaitAsync(token).ConfigureAwait(false);
            Interlocked.Increment(ref activeVerify);
            _metrics.SetActiveVerify(Volatile.Read(ref activeVerify));
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
                    UpdateLastProcessed(move.Key);
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeVerify);
                _metrics.SetActiveVerify(Volatile.Read(ref activeVerify));
                verifySemaphore.Release();
            }
        }

        async Task SwapBatchAsync(List<KeyMove<TKey>> batch, CancellationToken token)
        {
            if (batch.Count == 0)
            {
                return;
            }
            foreach (var m in batch)
            {
                if (states[m.Key] == KeyMoveState.Verified)
                {
                    states[m.Key] = KeyMoveState.Swapping;
                }
            }

            await ExecuteWithRetry(() => _swapper.SwapAsync(batch, token), "swap", batch[0]).ConfigureAwait(false);

            foreach (var m in batch)
            {
                if (states[m.Key] == KeyMoveState.Swapping)
                {
                    states[m.Key] = KeyMoveState.Done;
                    _metrics.IncSwapped();
                    transitionSinceFlush++;
                    UpdateLastProcessed(m.Key);
                }
            }
        }
    }
}
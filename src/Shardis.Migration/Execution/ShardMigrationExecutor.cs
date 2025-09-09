
using System.Collections.Concurrent;
using System.Diagnostics;

using Shardis.Diagnostics;
using Shardis.Logging;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Execution;
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
    IShardisLogger? logger = null,
    Func<DateTimeOffset>? timeProvider = null)
    where TKey : notnull, IEquatable<TKey>
{
    // NOTE: This class is intentionally a lightweight record-like primary-constructor type
    // with dependencies provided via the parameter list above. The implementation below
    // orchestrates the three phases of a key migration: copy, optional verify and batched swap.
    // It is designed to be deterministic, concurrency-safe, and checkpoint-friendly so that
    // long-running migrations can be resumed after failures.
    private readonly IShardDataMover<TKey> _mover = mover;
    private readonly IVerificationStrategy<TKey> _verification = verification;
    private readonly IShardMapSwapper<TKey> _swapper = swapper;
    private readonly IShardMigrationCheckpointStore<TKey> _checkpointStore = checkpointStore;
    private readonly IShardMigrationMetrics _metrics = metrics;
    private readonly ShardMigrationOptions _options = options;
    private readonly Func<DateTimeOffset> _now = timeProvider ?? (() => DateTimeOffset.UtcNow);
    private readonly IShardisLogger _log = logger ?? NullShardisLogger.Instance;

    private const int CheckpointVersion = 1;
    private static readonly ActivitySource Activity = ShardisDiagnostics.ActivitySource;
    /// <summary>Holds the last exception encountered while persisting a checkpoint in the failure path (for diagnostics).</summary>
    public Exception? LastCheckpointPersistException { get; private set; }

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
        // Capture start time + root activity for this plan execution.
        var started = _now();
        using var root = Activity.StartActivity("shardis.migration.execute", ActivityKind.Internal);
        root?.SetTag("migration.plan_id", plan.PlanId);
        root?.SetTag("migration.total_keys", plan.Moves.Count);

        // Attempt to load an existing checkpoint for this plan. A checkpoint contains
        // the last persisted move states and the last processed index so the executor
        // can resume without re-copying or re-verifying completed work.
        var checkpoint = await _checkpointStore.LoadAsync(plan.PlanId, ct).ConfigureAwait(false);

        // States map tracks the per-key state machine. If a checkpoint exists we resume
        // from the persisted states, otherwise initialize all keys as Planned.
        var states = checkpoint?.States is { Count: > 0 }
            ? new Dictionary<ShardKey<TKey>, KeyMoveState>(checkpoint.States)
            : plan.Moves.ToDictionary(m => m.Key, _ => KeyMoveState.Planned);

        // Only increment the planned counter when this is the first run (no checkpoint).
        if (checkpoint is null)
        {
            _metrics.IncPlanned(states.Count);
        }

        // Tracks the highest index that progressed successfully. Used for idempotent
        // checkpointing and progress reporting. -1 means nothing processed yet.
        var lastProcessedIndex = checkpoint?.LastProcessedIndex ?? -1;
        var total = plan.Moves.Count;

        // Map each key to its index in the plan for quick lookups when updating the
        // "lastProcessedIndex" using a bounded CAS loop.
        var indexByKey = new Dictionary<ShardKey<TKey>, int>(plan.Moves.Count);
        for (int ii = 0; ii < plan.Moves.Count; ii++)
        {
            indexByKey[plan.Moves[ii].Key] = ii;
        }

        // Pending batch prepared for the swap phase. We accumulate verified keys here
        // until we reach SwapBatchSize or the plan completes.
        var pendingSwap = new List<KeyMove<TKey>>();

        // Counters used to decide when to persist checkpoints. We persist when
        // transitionSinceFlush is large enough or when an interval elapses.
        var transitionSinceFlush = 0;
        var lastFlushAt = _now();
        var lastProgressAt = DateTimeOffset.MinValue;

        // Concurrency accounting for metrics and semaphores that bound work.
        int activeCopy = 0, activeVerify = 0;
        var copySemaphore = new SemaphoreSlim(_options.CopyConcurrency);
        var verifySemaphore = new SemaphoreSlim(_options.VerifyConcurrency);

        // Tracks copy tasks currently in-flight so we can throttle and await completion.
        var inFlightCopies = new List<Task>();

        // Lightweight set of keys that failed permanently (used only for diagnostics here).
        var failedKeys = new ConcurrentDictionary<ShardKey<TKey>, byte>();

        // Atomically advance the lastProcessedIndex for progress reporting and
        // checkpointing. We only progress forward; a bounded CAS loop protects
        // against races with multiple concurrent workers touching different keys.
        void UpdateLastProcessed(ShardKey<TKey> key)
        {
            if (indexByKey.TryGetValue(key, out var idx))
            {
                // Bounded CAS loop: contention expected to be extremely low; guard against pathological spinning.
                const int maxSpins = 10_000;
                var spins = 0;
                while (true)
                {
                    var snapshot = lastProcessedIndex;
                    if (idx <= snapshot)
                    {
                        // Another worker already advanced or we are not far enough to update.
                        return;
                    }

                    var updated = Interlocked.CompareExchange(ref lastProcessedIndex, idx, snapshot);

                    if (updated == snapshot)
                    {
                        // Successfully advanced.
                        return;
                    }

                    if (++spins == maxSpins)
                    {
                        // Give up after many spins; progress will be retried later.
                        return;
                    }
                }
            }
        }

        // Generic helper that executes an arbitrary async action with exponential
        // backoff retries. On permanent failure (after retries exhausted) the key
        // is marked as Failed and metrics/state are updated accordingly.
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
                    // Transient failure: increment retry metric and wait with capped exponential backoff.
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
                    // Permanent failure path: mark the move as failed and ensure progress & metrics.
                    failedKeys.TryAdd(move.Key, 0);
                    states[move.Key] = KeyMoveState.Failed;
                    _metrics.IncFailed();
                    transitionSinceFlush++;
                    UpdateLastProcessed(move.Key);
                    return;
                }
            }
        }

        // Throttled progress emitter (at most once per second) unless force=true.
        void EmitProgress(bool force)
        {
            if (progress is null)
            {
                return;
            }

            var now = _now();

            if (!force && now - lastProgressAt < TimeSpan.FromSeconds(1))
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

        // Persist a checkpoint when thresholds are met or when forced. Checkpoints allow
        // the executor to resume work without repeating completed phases.
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
            // Phase 1: Copy. Iterate plan moves and start copy tasks up to copy concurrency.
            for (int i = 0; i < plan.Moves.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var move = plan.Moves[i];

                // Skip work already copy-or-beyond (resumed from checkpoint).
                if (states[move.Key] >= KeyMoveState.Copied)
                {
                    if (states[move.Key] == KeyMoveState.Verified)
                    {
                        // Already verified => candidate for swap.
                        pendingSwap.Add(move);
                    }
                    continue;
                }

                // Throttle concurrency using semaphore.
                await copySemaphore.WaitAsync(ct).ConfigureAwait(false);

                Interlocked.Increment(ref activeCopy);
                _metrics.SetActiveCopy(Volatile.Read(ref activeCopy));

                // Start the actual copy work on the thread-pool so we can continue scheduling
                // further copies without awaiting each immediately.
                var copyTask = Task.Run(async () =>
                {
                    using var span = Activity.StartActivity("shardis.migration.copy", ActivityKind.Internal);
                    span?.SetTag("migration.phase", "copy");
                    span?.SetTag("shard.from", move.Source.Value);
                    span?.SetTag("shard.to", move.Target.Value);
                    span?.SetTag("migration.key", move.Key.Value?.ToString());
                    var copyStarted = Stopwatch.GetTimestamp();
                    try
                    {
                        states[move.Key] = KeyMoveState.Copying;
                        await ExecuteWithRetry(() => _mover.CopyAsync(move, ct), "copy", move).ConfigureAwait(false);
                        if (states[move.Key] != KeyMoveState.Failed)
                        {
                            // Copy succeeded; transition to Copied and update metrics.
                            states[move.Key] = KeyMoveState.Copied;
                            _metrics.IncCopied();
                            var elapsedMs = ElapsedMs(copyStarted);
                            _metrics.ObserveCopyDuration(elapsedMs);
                            transitionSinceFlush++;
                        }
                    }
                    finally
                    {
                        // Release accounting and allow another copy to start.
                        Interlocked.Decrement(ref activeCopy);
                        _metrics.SetActiveCopy(Volatile.Read(ref activeCopy));
                        copySemaphore.Release();
                    }

                    // If interleaving is enabled, start verification immediately for this key.
                    if (_options.InterleaveCopyAndVerify && states[move.Key] == KeyMoveState.Copied)
                    {
                        await StartVerifyAsync(move, ct).ConfigureAwait(false);
                    }
                }, ct);

                inFlightCopies.Add(copyTask);

                // If we've started as many copies as allowed, await one to complete so we can schedule more.
                if (inFlightCopies.Count >= _options.CopyConcurrency)
                {
                    var finished = await Task.WhenAny(inFlightCopies).ConfigureAwait(false);
                    inFlightCopies.Remove(finished);
                }

                EmitProgress(false);

                // Periodically persist checkpoints so long-running operations can be resumed.
                await PersistCheckpointIfNeeded(false, ct).ConfigureAwait(false);
            }

            // Wait for outstanding copy operations to finish.
            await Task.WhenAll(inFlightCopies).ConfigureAwait(false);

            // Phase 2: Verification for all copied keys (if not interleaved already).
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

            EmitProgress(false);

            // Prepare swap batches from verified keys and execute in batches.
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

            // Force a final checkpoint to capture completion state.
            await PersistCheckpointIfNeeded(true, ct).ConfigureAwait(false);
            EmitProgress(true);

            var done = states.Values.Count(s => s == KeyMoveState.Done);
            var failed = states.Values.Count(s => s == KeyMoveState.Failed);
            completed = true;

            var elapsed = _now() - started;
            _metrics.ObserveTotalElapsed(elapsed.TotalMilliseconds);
            return new MigrationSummary(plan.PlanId, total, done, failed, elapsed);
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
                catch (Exception ex)
                {
                    // Intentionally do not throw to avoid masking the original failure / cancellation exception.
                    // Store for diagnostic inspection; caller may choose to surface externally.
                    LastCheckpointPersistException = ex;
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
                using var span = Activity.StartActivity("shardis.migration.verify", ActivityKind.Internal);
                span?.SetTag("migration.phase", "verify");
                span?.SetTag("shard.from", move.Source.Value);
                span?.SetTag("shard.to", move.Target.Value);
                span?.SetTag("migration.key", move.Key.Value?.ToString());

                var verifyStarted = Stopwatch.GetTimestamp();
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
                    _metrics.ObserveVerifyDuration(ElapsedMs(verifyStarted));
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

            var swapStarted = Stopwatch.GetTimestamp();
            using var span = Activity.StartActivity("shardis.migration.swap_batch", ActivityKind.Internal);
            span?.SetTag("migration.phase", "swap");
            span?.SetTag("migration.batch_size", batch.Count);
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
            _metrics.ObserveSwapBatchDuration(ElapsedMs(swapStarted));
        }
    }

    private static double ElapsedMs(long startTimestamp)
    {
        var end = Stopwatch.GetTimestamp();
        return (end - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}
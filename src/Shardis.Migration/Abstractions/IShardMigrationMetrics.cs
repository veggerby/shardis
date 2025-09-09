namespace Shardis.Migration.Abstractions;

/// <summary>
/// Metrics instrumentation points for shard key migration lifecycle.
/// Implementations must be thread-safe and avoid heavy work in counters.
/// </summary>
public interface IShardMigrationMetrics
{
    /// <summary>Increments the number of keys planned.</summary>
    /// <param name="delta">The increment (default 1).</param>
    void IncPlanned(long delta = 1);

    /// <summary>Increments the number of keys copied.</summary>
    /// <param name="delta">The increment (default 1).</param>
    void IncCopied(long delta = 1);

    /// <summary>Increments the number of keys verified OK.</summary>
    /// <param name="delta">The increment (default 1).</param>
    void IncVerified(long delta = 1);

    /// <summary>Increments the number of keys swapped into the new map.</summary>
    /// <param name="delta">The increment (default 1).</param>
    void IncSwapped(long delta = 1);

    /// <summary>Increments the number of keys that failed permanently.</summary>
    /// <param name="delta">The increment (default 1).</param>
    void IncFailed(long delta = 1);

    /// <summary>Increments the number of retries performed.</summary>
    /// <param name="delta">The increment (default 1).</param>
    void IncRetries(long delta = 1);

    /// <summary>Sets the current number of in-flight copy operations.</summary>
    /// <param name="value">The current copy concurrency.</param>
    void SetActiveCopy(int value);

    /// <summary>Sets the current number of in-flight verification operations.</summary>
    /// <param name="value">The current verify concurrency.</param>
    void SetActiveVerify(int value);

    /// <summary>Records the duration of the copy phase for a single key (in milliseconds).</summary>
    /// <param name="ms">Elapsed milliseconds.</param>
    void ObserveCopyDuration(double ms);

    /// <summary>Records the duration of the verify phase for a single key (in milliseconds).</summary>
    /// <param name="ms">Elapsed milliseconds.</param>
    void ObserveVerifyDuration(double ms);

    /// <summary>Records the duration of each swap batch (in milliseconds).</summary>
    /// <param name="ms">Elapsed milliseconds.</param>
    void ObserveSwapBatchDuration(double ms);

    /// <summary>Records total execution elapsed once a plan completes (in milliseconds).</summary>
    /// <param name="ms">Elapsed milliseconds.</param>
    void ObserveTotalElapsed(double ms);
}
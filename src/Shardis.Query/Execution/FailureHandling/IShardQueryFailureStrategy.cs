namespace Shardis.Query.Execution.FailureHandling;

/// <summary>
/// Defines how shard-level exceptions are handled during distributed query execution.
/// </summary>
public interface IShardQueryFailureStrategy
{
    /// <summary>
    /// Handle a shard exception. Return <c>true</c> if execution should continue processing remaining shards; <c>false</c> if it should stop.
    /// Implementations may rethrow to fail immediately.
    /// </summary>
    bool OnShardException(Exception ex, int shardIndex);
}

/// <summary>
/// Fail immediately on the first shard exception.
/// </summary>
public sealed class FailFastFailureStrategy : IShardQueryFailureStrategy
{
    /// <summary>Singleton instance.</summary>
    public static readonly FailFastFailureStrategy Instance = new();
    private FailFastFailureStrategy() { }

    /// <inheritdoc />
    public bool OnShardException(Exception ex, int shardIndex)
    {
        // rethrow preserving original stack
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        return false; // unreachable
    }
}

/// <summary>
/// Collect shard exceptions and continue. Enumeration completes yielding only successful items.
/// AggregateException is thrown only if ALL shards fail (to signal total failure) to avoid silent empty result.
/// </summary>
public sealed class BestEffortFailureStrategy : IShardQueryFailureStrategy
{
    /// <summary>Singleton instance.</summary>
    public static readonly BestEffortFailureStrategy Instance = new();
    private BestEffortFailureStrategy() { }

    private readonly List<Exception> _failures = new();
    private int _shardCount;
    private int _successCount;

    /// <summary>Initialize internal counters for a new execution (not currently invoked by wrapper; reserved for future per-shard integration).</summary>
    public void Initialize(int shardCount)
    {
        _shardCount = shardCount;
        _successCount = 0;
        _failures.Clear();
    }

    /// <inheritdoc />
    public bool OnShardException(Exception ex, int shardIndex)
    {
        lock (_failures)
        {
            _failures.Add(ex);
        }
        // continue processing others
        return true;
    }

    /// <summary>Record successful shard completion.</summary>
    public void OnShardCompleted()
    {
        Interlocked.Increment(ref _successCount);
    }

    /// <summary>Throw aggregate if all shards failed (no successes) to avoid silent empty output.</summary>
    public void FinalizeOrThrow()
    {
        if (_successCount == 0 && _failures.Count > 0)
        {
            throw new AggregateException("All shards failed", _failures.ToArray());
        }
    }
}

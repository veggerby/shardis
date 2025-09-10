namespace Shardis.Query.Execution.FailureHandling;

/// <summary>Defines how shard-level exceptions are handled during distributed query execution.</summary>
public interface IShardQueryFailureStrategy
{
    /// <summary>Handle a shard exception. Return true to continue (best-effort), false to stop (fail-fast). Strategy may rethrow.</summary>
    bool OnShardException(Exception ex, int shardIndex);
}

/// <summary>Fail immediately on the first shard exception.</summary>
public sealed class FailFastFailureStrategy : IShardQueryFailureStrategy
{
    /// <summary>Singleton instance.</summary>
    public static readonly FailFastFailureStrategy Instance = new();
    private FailFastFailureStrategy() { }

    /// <inheritdoc />
    public bool OnShardException(Exception ex, int shardIndex)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        return false; // unreachable
    }
}

/// <summary>Collect shard exceptions and continue enumerating remaining shards.</summary>
public sealed class BestEffortFailureStrategy : IShardQueryFailureStrategy
{
    /// <summary>Singleton instance.</summary>
    public static readonly BestEffortFailureStrategy Instance = new();
    private BestEffortFailureStrategy() { }

    private readonly List<Exception> _failures = new();
    private int _successCount;

    /// <inheritdoc />
    public bool OnShardException(Exception ex, int shardIndex)
    {
        lock (_failures) { _failures.Add(ex); }
        return true; // swallow & continue
    }

    /// <summary>Record success path (optional external hook).</summary>
    public void OnShardSuccess() => Interlocked.Increment(ref _successCount);

    /// <summary>Throw aggregate if every shard failed (prevents silent empty result).</summary>
    public void FinalizeOrThrow(int totalShards)
    {
        if (_successCount == 0 && _failures.Count > 0)
        {
            throw new AggregateException("All shards failed", _failures.ToArray());
        }
    }
}
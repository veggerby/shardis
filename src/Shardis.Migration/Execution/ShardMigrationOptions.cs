namespace Shardis.Migration.Execution;

/// <summary>
/// Options controlling shard key migration executor behavior.
/// </summary>
public sealed class ShardMigrationOptions
{
    private const int MaxAllowedConcurrency = 1024;

    private int _copyConcurrency = 32;
    private int _verifyConcurrency = 32;
    private int _swapBatchSize = 500;
    private int _maxRetries = 5;
    private TimeSpan _retryBaseDelay = TimeSpan.FromMilliseconds(100);
    private TimeSpan _checkpointFlushInterval = TimeSpan.FromSeconds(2);
    private int _checkpointFlushEveryTransitions = 1000;
    private TimeSpan _healthWindow = TimeSpan.FromSeconds(5);
    private TimeSpan _maxReadStaleness = TimeSpan.FromSeconds(2);

    /// <summary>Overall soft cap for concurrent key moves (copy+verify units). If set, may be used by an external governor.</summary>
    public int? MaxConcurrentMoves { get; init; }

    /// <summary>Maximum in-flight moves per shard (advisory; enforced by budget governor when present).</summary>
    public int? MaxMovesPerShard { get; init; }

    /// <summary>Maximum simultaneous copy operations.</summary>
    public int CopyConcurrency
    {
        get => _copyConcurrency;
        init => _copyConcurrency = ValidatePositiveBounded(value, nameof(CopyConcurrency), MaxAllowedConcurrency);
    }

    /// <summary>Maximum simultaneous verify operations.</summary>
    public int VerifyConcurrency
    {
        get => _verifyConcurrency;
        init => _verifyConcurrency = ValidatePositiveBounded(value, nameof(VerifyConcurrency), MaxAllowedConcurrency);
    }

    /// <summary>Maximum number of verified keys applied in a single swap batch.</summary>
    public int SwapBatchSize
    {
        get => _swapBatchSize;
        init => _swapBatchSize = ValidatePositiveBounded(value, nameof(SwapBatchSize), 100_000);
    }

    /// <summary>Maximum retry attempts for transient operations (copy/verify/swap).</summary>
    public int MaxRetries
    {
        get => _maxRetries;
        init => _maxRetries = ValidateNonNegative(value, nameof(MaxRetries));
    }

    /// <summary>Base delay used for exponential backoff (delay * 2^attempt).</summary>
    public TimeSpan RetryBaseDelay
    {
        get => _retryBaseDelay;
        init => _retryBaseDelay = ValidatePositive(value, nameof(RetryBaseDelay));
    }

    /// <summary>If true, copy and verify phases are interleaved; otherwise staged sequentially.</summary>
    public bool InterleaveCopyAndVerify { get; init; } = true;

    /// <summary>Enables dry-run hash sampling (future feature hook).</summary>
    public bool EnableDryRunHashSampling { get; init; } = true;

    /// <summary>If true, allows swap even if verification failed (for diagnostics / forced failover scenarios).</summary>
    public bool ForceSwapOnVerificationFailure { get; init; } = false;

    /// <summary>If true, read path may consult both source and target shard during an in-progress move for a key.</summary>
    public bool EnableDualRead { get; init; } = false;

    /// <summary>If true, write path duplicates writes to source and target during a move window (higher cost).</summary>
    public bool EnableDualWrite { get; init; } = false;

    /// <summary>Interval for periodic checkpoint flush (wall clock).</summary>
    public TimeSpan CheckpointFlushInterval
    {
        get => _checkpointFlushInterval;
        init => _checkpointFlushInterval = ValidatePositive(value, nameof(CheckpointFlushInterval));
    }

    /// <summary>Flush checkpoint after this many state transitions (best-effort).</summary>
    public int CheckpointFlushEveryTransitions
    {
        get => _checkpointFlushEveryTransitions;
        init => _checkpointFlushEveryTransitions = ValidatePositiveBounded(value, nameof(CheckpointFlushEveryTransitions), 1_000_000);
    }

    /// <summary>Observation window for health evaluation (latency / mismatch).</summary>
    public TimeSpan HealthWindow
    {
        get => _healthWindow;
        init => _healthWindow = ValidatePositive(value, nameof(HealthWindow));
    }

    /// <summary>Target upper bound (p99) for read staleness when dual-read disabled.</summary>
    public TimeSpan MaxReadStaleness
    {
        get => _maxReadStaleness;
        init => _maxReadStaleness = ValidatePositive(value, nameof(MaxReadStaleness));
    }

    private static int ValidatePositiveBounded(int value, string name, int max)
    {
        if (value <= 0 || value > max)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be between 1 and {max}.");
        }
        return value;
    }

    private static int ValidateNonNegative(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be non-negative.");
        }
        return value;
    }

    private static TimeSpan ValidatePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
        }
        return value;
    }
}
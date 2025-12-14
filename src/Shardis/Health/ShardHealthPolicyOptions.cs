namespace Shardis.Health;

/// <summary>
/// Configuration options for the default periodic health policy.
/// </summary>
public sealed record ShardHealthPolicyOptions
{
    /// <summary>
    /// Gets the interval between periodic health probes.
    /// </summary>
    /// <remarks>Default: 30 seconds.</remarks>
    public TimeSpan ProbeInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the timeout for individual health probe operations.
    /// </summary>
    /// <remarks>Default: 5 seconds.</remarks>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the number of consecutive failures before marking a shard as unhealthy.
    /// </summary>
    /// <remarks>Default: 3.</remarks>
    public int UnhealthyThreshold { get; init; } = 3;

    /// <summary>
    /// Gets the number of consecutive successes before marking an unhealthy shard as healthy again.
    /// </summary>
    /// <remarks>Default: 2.</remarks>
    public int HealthyThreshold { get; init; } = 2;

    /// <summary>
    /// Gets the cooldown period after marking a shard unhealthy before attempting recovery probes.
    /// </summary>
    /// <remarks>Default: 60 seconds.</remarks>
    public TimeSpan CooldownPeriod { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets a value indicating whether reactive health tracking is enabled (recording successes/failures from operations).
    /// </summary>
    /// <remarks>Default: true.</remarks>
    public bool ReactiveTrackingEnabled { get; init; } = true;
}

namespace Shardis.Query.Health;

/// <summary>
/// Configuration options for health-aware query execution.
/// </summary>
public sealed record HealthAwareQueryOptions
{
    /// <summary>
    /// Gets the default options (include all shards, no health filtering).
    /// </summary>
    public static readonly HealthAwareQueryOptions Default = new();

    /// <summary>
    /// Gets options for best-effort mode (skip unhealthy shards).
    /// </summary>
    public static readonly HealthAwareQueryOptions BestEffort = new()
    {
        Behavior = UnhealthyShardBehavior.Skip,
        AvailabilityRequirement = ShardAvailabilityRequirement.BestEffort
    };

    /// <summary>
    /// Gets options for strict mode (fail if any shard is unhealthy).
    /// </summary>
    public static readonly HealthAwareQueryOptions Strict = new()
    {
        Behavior = UnhealthyShardBehavior.Quarantine,
        AvailabilityRequirement = ShardAvailabilityRequirement.AllShards
    };

    /// <summary>
    /// Gets the behavior when encountering unhealthy shards.
    /// </summary>
    /// <remarks>Default: <see cref="UnhealthyShardBehavior.Include"/>.</remarks>
    public UnhealthyShardBehavior Behavior { get; init; } = UnhealthyShardBehavior.Include;

    /// <summary>
    /// Gets the minimum shard availability requirement.
    /// </summary>
    /// <remarks>Default: <see cref="ShardAvailabilityRequirement.BestEffort"/>.</remarks>
    public ShardAvailabilityRequirement AvailabilityRequirement { get; init; } = ShardAvailabilityRequirement.BestEffort;

    /// <summary>
    /// Creates options that require a minimum number of healthy shards.
    /// </summary>
    public static HealthAwareQueryOptions RequireMinimum(int minHealthyShards)
        => new()
        {
            Behavior = UnhealthyShardBehavior.Skip,
            AvailabilityRequirement = ShardAvailabilityRequirement.AtLeast(minHealthyShards)
        };

    /// <summary>
    /// Creates options that require a minimum percentage of healthy shards.
    /// </summary>
    public static HealthAwareQueryOptions RequirePercentage(double minPercentage)
        => new()
        {
            Behavior = UnhealthyShardBehavior.Skip,
            AvailabilityRequirement = ShardAvailabilityRequirement.AtLeastPercentage(minPercentage)
        };
}

namespace Shardis.Query.Health;

/// <summary>
/// Defines minimum shard availability requirements for query execution.
/// </summary>
public sealed record ShardAvailabilityRequirement
{
    /// <summary>
    /// Gets a requirement that allows queries with any number of healthy shards (best-effort).
    /// </summary>
    public static readonly ShardAvailabilityRequirement BestEffort = new() { MinimumHealthyShards = 0 };

    /// <summary>
    /// Gets a requirement that all targeted shards must be healthy (strict mode).
    /// </summary>
    public static readonly ShardAvailabilityRequirement AllShards = new() { RequireAllHealthy = true };

    /// <summary>
    /// Gets the minimum number of healthy shards required to proceed with query execution.
    /// </summary>
    /// <remarks>Default: 0 (best-effort).</remarks>
    public int MinimumHealthyShards { get; init; }

    /// <summary>
    /// Gets the minimum percentage of healthy shards required (0.0 to 1.0).
    /// </summary>
    /// <remarks>Default: null (not enforced).</remarks>
    public double? MinimumHealthyPercentage { get; init; }

    /// <summary>
    /// Gets a value indicating whether all targeted shards must be healthy.
    /// </summary>
    /// <remarks>Default: false.</remarks>
    public bool RequireAllHealthy { get; init; }

    /// <summary>
    /// Creates a requirement for a specific minimum number of healthy shards.
    /// </summary>
    public static ShardAvailabilityRequirement AtLeast(int count)
        => new() { MinimumHealthyShards = count };

    /// <summary>
    /// Creates a requirement for a specific minimum percentage of healthy shards.
    /// </summary>
    public static ShardAvailabilityRequirement AtLeastPercentage(double percentage)
        => new() { MinimumHealthyPercentage = percentage };
}

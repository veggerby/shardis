using Microsoft.EntityFrameworkCore;

namespace Shardis.Query.EntityFrameworkCore;

/// <summary>
/// Optional execution tuning options for EF Core shard query executors.
/// All values are hints; null means framework default.
/// </summary>
public sealed class EfCoreExecutionOptions
{
    /// <summary>Optional maximum shard fan-out concurrency (null = internal default).</summary>
    public int? Concurrency { get; init; }

    /// <summary>Optional channel capacity for unordered merge (null = unbounded / implementation default).</summary>
    public int? ChannelCapacity { get; init; }

    /// <summary>Optional per-shard database command timeout.</summary>
    public TimeSpan? PerShardCommandTimeout { get; init; }

    /// <summary>Dispose DbContext after each shard query (default true). If false you must manage lifetime externally.</summary>
    public bool DisposeContextPerQuery { get; init; } = true;
}

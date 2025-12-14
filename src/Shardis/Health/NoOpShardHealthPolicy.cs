using Shardis.Model;

namespace Shardis.Health;

/// <summary>
/// No-op health policy that always reports all shards as healthy.
/// </summary>
/// <remarks>
/// This is the default implementation when health monitoring is not explicitly configured.
/// </remarks>
public sealed class NoOpShardHealthPolicy : IShardHealthPolicy
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NoOpShardHealthPolicy Instance = new();

    private NoOpShardHealthPolicy() { }

    /// <inheritdoc />
    public ValueTask<ShardHealthReport> GetHealthAsync(ShardId shardId, CancellationToken ct = default)
    {
        return ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = shardId,
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow,
            Description = "No health monitoring configured"
        });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShardHealthReport> GetAllHealthAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield break;
    }

    /// <inheritdoc />
    public ValueTask RecordSuccessAsync(ShardId shardId, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RecordFailureAsync(ShardId shardId, Exception exception, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ShardHealthReport> ProbeAsync(ShardId shardId, CancellationToken ct = default)
    {
        return GetHealthAsync(shardId, ct);
    }
}

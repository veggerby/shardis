using Shardis.Health;
using Shardis.Model;
using Shardis.Query.Execution;

namespace Shardis.Query.Health;

/// <summary>
/// Query executor wrapper that applies health-aware shard filtering based on a health policy.
/// </summary>
/// <remarks>
/// This executor consults the configured <see cref="IShardHealthPolicy"/> to determine which shards
/// should be included in query execution. Behavior is controlled by <see cref="HealthAwareQueryOptions"/>.
/// </remarks>
internal sealed class HealthAwareQueryExecutor : IShardQueryExecutor
{
    private readonly IShardQueryExecutor _inner;
    private readonly IShardHealthPolicy _healthPolicy;
    private readonly HealthAwareQueryOptions _options;

    public HealthAwareQueryExecutor(
        IShardQueryExecutor inner,
        IShardHealthPolicy healthPolicy,
        HealthAwareQueryOptions options)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _healthPolicy = healthPolicy ?? throw new ArgumentNullException(nameof(healthPolicy));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IShardQueryCapabilities Capabilities => _inner.Capabilities;

    public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(
        QueryModel model,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var targetShardIds = model.TargetShards?.ToList() ?? new List<ShardId>();
        
        if (targetShardIds.Count == 0 || _options.Behavior == UnhealthyShardBehavior.Include)
        {
            await foreach (var item in _inner.ExecuteAsync<TResult>(model, ct).ConfigureAwait(false))
            {
                yield return item;
            }
            yield break;
        }

        var healthyShardIds = new List<ShardId>();
        var unhealthyShardIds = new List<ShardId>();

        foreach (var shardId in targetShardIds)
        {
            var health = await _healthPolicy.GetHealthAsync(shardId, ct).ConfigureAwait(false);
            if (health.Status == ShardHealthStatus.Healthy)
            {
                healthyShardIds.Add(shardId);
            }
            else
            {
                unhealthyShardIds.Add(shardId);
            }
        }

        if (_options.Behavior == UnhealthyShardBehavior.Quarantine && unhealthyShardIds.Count > 0)
        {
            throw new InsufficientHealthyShardsException(
                targetShardIds.Count,
                healthyShardIds.Count,
                unhealthyShardIds,
                $"Query requires all shards to be healthy, but {unhealthyShardIds.Count} shard(s) are unhealthy: {string.Join(", ", unhealthyShardIds)}");
        }

        if (!ValidateAvailability(targetShardIds.Count, healthyShardIds.Count, unhealthyShardIds))
        {
            throw new InsufficientHealthyShardsException(
                targetShardIds.Count,
                healthyShardIds.Count,
                unhealthyShardIds,
                $"Insufficient healthy shards: {healthyShardIds.Count}/{targetShardIds.Count} available (requirement: {FormatRequirement()})");
        }

        if (healthyShardIds.Count == 0)
        {
            yield break;
        }

        var filteredModel = model.WithTargetShards(healthyShardIds);

        await foreach (var item in _inner.ExecuteAsync<TResult>(filteredModel, ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private bool ValidateAvailability(int totalShards, int healthyCount, List<ShardId> unhealthyShardIds)
    {
        var requirement = _options.AvailabilityRequirement;

        if (requirement.RequireAllHealthy)
        {
            return unhealthyShardIds.Count == 0;
        }

        if (healthyCount < requirement.MinimumHealthyShards)
        {
            return false;
        }

        if (requirement.MinimumHealthyPercentage.HasValue)
        {
            var percentage = (double)healthyCount / totalShards;
            if (percentage < requirement.MinimumHealthyPercentage.Value)
            {
                return false;
            }
        }

        return true;
    }

    private string FormatRequirement()
    {
        var req = _options.AvailabilityRequirement;
        if (req.RequireAllHealthy)
        {
            return "all shards";
        }
        if (req.MinimumHealthyPercentage.HasValue)
        {
            return $"{req.MinimumHealthyPercentage.Value:P0}";
        }
        return $"at least {req.MinimumHealthyShards}";
    }
}

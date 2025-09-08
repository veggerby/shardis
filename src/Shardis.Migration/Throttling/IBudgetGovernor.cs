namespace Shardis.Migration.Throttling;

/// <summary>Interface for dynamic budget governance (adaptive concurrency / backpressure).</summary>
public interface IBudgetGovernor
{
    /// <summary>Current effective global concurrent operation budget.</summary>
    int CurrentGlobalBudget { get; }

    /// <summary>Maximum allowed concurrent operations per shard.</summary>
    int MaxPerShardBudget { get; }

    /// <summary>Reports latest shard health metrics to influence future recalculations.</summary>
    /// <param name="health">Shard health snapshot.</param>
    void Report(ShardHealth health);

    /// <summary>Recalculates global budget based on reported health (idempotent, inexpensive).</summary>
    void Recalculate();

    /// <summary>Attempts to acquire a concurrency slot for a shard.</summary>
    /// <param name="token">Opaque token representing the acquired slot.</param>
    /// <param name="shardId">Shard identifier string.</param>
    /// <returns>True if acquired; false if capacity exceeded.</returns>
    bool TryAcquire(out object token, string shardId);

    /// <summary>Releases a previously acquired concurrency slot.</summary>
    /// <param name="token">Token from <see cref="TryAcquire"/>.</param>
    /// <param name="shardId">Shard identifier.</param>
    void Release(object token, string shardId);
}

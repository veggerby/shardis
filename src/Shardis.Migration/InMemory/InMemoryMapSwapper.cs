namespace Shardis.Migration.InMemory;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.Persistence;

/// <summary>
/// In-memory implementation that applies shard ownership swaps via the underlying map store.
/// Simulates partial failure when <see cref="SimulatePartialFailure"/> is true by failing after half the batch.
/// </summary>
internal sealed class InMemoryMapSwapper<TKey> : IShardMapSwapper<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardMapStore<TKey> _mapStore;
    public bool SimulatePartialFailure { get; set; }

    public InMemoryMapSwapper(IShardMapStore<TKey> mapStore)
    {
        _mapStore = mapStore;
    }

    public Task SwapAsync(IReadOnlyList<KeyMove<TKey>> verifiedBatch, CancellationToken ct)
    {
        if (verifiedBatch.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Apply assignments sequentially (in-memory store CAS semantics handled internally if needed).
        var half = verifiedBatch.Count / 2;
        for (int i = 0; i < verifiedBatch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var move = verifiedBatch[i];
            _mapStore.AssignShardToKey(move.Key, move.Target);
            if (SimulatePartialFailure && i == half - 1)
            {
                throw new InvalidOperationException("Simulated partial failure after half batch applied.");
            }
        }

        return Task.CompletedTask;
    }
}
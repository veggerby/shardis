using Shardis.Model;

namespace Shardis.Querying;
public interface IShardisEnumerator<T> : IAsyncEnumerator<ShardItem<T>>
{
    /// <summary>
    /// Returns the total number of shards this enumerator is consuming.
    /// </summary>
    int ShardCount { get; }

    /// <summary>
    /// True if all shards have completed.
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// True if the enumerator has produced at least one value.
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// True if MoveNextAsync has ever been called.
    /// </summary>
    bool IsPrimed { get; }
}

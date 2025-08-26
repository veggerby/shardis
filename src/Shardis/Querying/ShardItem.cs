using Shardis.Model;

namespace Shardis.Querying;

/// <summary>
/// Represents a value originating from a specific shard, preserving origin metadata during merges.
/// </summary>
/// <typeparam name="TItem">The underlying item type.</typeparam>
/// <param name="ShardId">Origin shard identifier.</param>
/// <param name="Item">The value produced by the shard.</param>
public readonly record struct ShardItem<TItem>(ShardId ShardId, TItem Item);
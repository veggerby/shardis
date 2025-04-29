using Shardis.Model;

namespace Shardis.Querying;

public readonly record struct ShardItem<TItem>(ShardId ShardId, TItem Item);

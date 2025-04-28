using Shardis.Model;

namespace Shardis.Routing;

public interface IShardRouter<TSession>
{
    /// <summary>
    /// Routes a given key to a shard.
    /// </summary>
    /// <param name="shardKey">The shard key representing an aggregate instance.</param>
    /// <returns>The shard that should handle the given key.</returns>
    IShard<TSession> RouteToShard(ShardKey shardKey);
}

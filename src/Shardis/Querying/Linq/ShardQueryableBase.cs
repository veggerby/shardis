using Shardis.Model;

namespace Shardis.Querying.Linq;

internal abstract class ShardQueryableBase<T> : IShardQueryable<T>
{
    public abstract IShardQueryable<T> GetSource<TSession>(IShard<TSession> session);
}
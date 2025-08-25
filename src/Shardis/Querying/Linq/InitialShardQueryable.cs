using Shardis.Model;

namespace Shardis.Querying.Linq;

internal class InitialShardQueryable<T> : ShardQueryableBase<T>
{
    public override IShardQueryable<T> GetSource<TSession>(IShard<TSession> session) => this;
}
namespace Shardis.Querying.Linq;

public interface IShardQueryOrchestrator
{
    Task<List<T>> ExecuteToListAsync<T>(ShardQueryPlan<T> plan, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> ExecuteAsyncEnumerable<T>(ShardQueryPlan<T> plan, CancellationToken cancellationToken = default);
}
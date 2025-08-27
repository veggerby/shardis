using Shardis.Query.Execution;

namespace Shardis.Query;

internal sealed class ShardQueryable<T> : IShardQueryable<T>
{
    public IShardQueryExecutor Executor { get; }
    public QueryModel Model { get; }

    public ShardQueryable(IShardQueryExecutor executor, QueryModel model)
    {
        Executor = executor ?? throw new ArgumentNullException(nameof(executor));
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
        => Executor.ExecuteAsync<T>(Model, ct).GetAsyncEnumerator(ct);
}
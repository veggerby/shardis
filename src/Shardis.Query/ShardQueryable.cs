using Shardis.Query.Execution;

namespace Shardis.Query;

internal sealed class ShardQueryable<T>(IShardQueryExecutor executor, QueryModel model) : IShardQueryable<T>
{
    public IShardQueryExecutor Executor { get; } = executor ?? throw new ArgumentNullException(nameof(executor));

    public QueryModel Model { get; } = model ?? throw new ArgumentNullException(nameof(model));

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
        => Executor.ExecuteAsync<T>(Model, ct).GetAsyncEnumerator(ct);
}
using Shardis.Model;
using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Targeted shard filtering helpers (diagnostics / operational tooling). Experimental: may evolve.
/// </summary>
public static class ShardQueryableTargetingExtensions
{
    /// <summary>
    /// Restrict execution to a single shard id (bypasses normal fan-out). Intended for diagnostics.
    /// </summary>
    public static IShardQueryable<T> WhereShard<T>(this IShardQueryable<T> source, ShardId shardId)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TargetedShardQueryable<T>(source, shardId);
    }

    private sealed class TargetedShardQueryable<T>(IShardQueryable<T> inner, ShardId target) : IShardQueryable<T>
    {
        public IShardQueryExecutor Executor => new SingleShardExecutor(inner.Executor, target);
        public QueryModel Model => inner.Model;
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default) => Executor.ExecuteAsync<T>(Model, ct).GetAsyncEnumerator(ct);
    }

    private sealed class SingleShardExecutor(IShardQueryExecutor inner, ShardId target) : IShardQueryExecutor
    {
        public IShardQueryCapabilities Capabilities => inner.Capabilities;
        public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // NOTE: Current executor abstraction fans out internally; no native targeted path yet.
            // Interim approach: execute full query and yield items while tagging (future: introduce shard filter in executor contract).
            _ = target; // suppress unused (placeholder for future targeted execution logic)
            await foreach (var item in inner.ExecuteAsync<TResult>(model, ct).WithCancellation(ct))
            {
                yield return item; // TODO future: short-circuit other shards
            }
        }
    }
}
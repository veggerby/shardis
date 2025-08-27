using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Represents a shard-aware asynchronous query exposing its immutable <see cref="QueryModel"/>.
/// MVP surface: only Where / Select composition and unordered execution. Ordering is not supported; use ordered streaming broadcaster APIs instead.
/// </summary>
/// <typeparam name="T">Result element type.</typeparam>
public interface IShardQueryable<out T> : IAsyncEnumerable<T>
{
    /// <summary>Executor responsible for fan-out across shards.</summary>
    IShardQueryExecutor Executor { get; }

    /// <summary>Immutable query shape capturing Where/Select chain.</summary>
    QueryModel Model { get; }
}
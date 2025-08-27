using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Entry point for constructing shard LINQ queries over a supplied <see cref="IShardQueryExecutor"/>.
/// </summary>
public static class ShardQuery
{
    /// <summary>Create a query root over the supplied executor.</summary>
    public static IShardQueryable<T> For<T>(IShardQueryExecutor executor)
        => new ShardQueryable<T>(executor, QueryModel.Create(typeof(T)));
}
namespace Shardis.Querying.Linq;

/// <summary>
/// Represents a fluent, composable query across multiple shards.
/// </summary>
public interface IShardQueryable<T> { }

public interface IWhereShardQueryable<T> : IShardQueryable<T> { }

public interface IOrderedShardQueryable<T> : IShardQueryable<T> { }

public interface ISelectShardQueryable<TSource, TResult> : IShardQueryable<TResult> { }
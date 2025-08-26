namespace Shardis.Querying.Linq;

/// <summary>
/// Marker for a shard-composed query (no provider infrastructure active yet).
/// </summary>
public interface IShardQueryable<T> { }

/// <summary>
/// Marker for a shard query after a Where stage.
/// </summary>
public interface IWhereShardQueryable<T> : IShardQueryable<T> { }

/// <summary>
/// Marker for a shard query after an OrderBy stage.
/// </summary>
public interface IOrderedShardQueryable<T> : IShardQueryable<T> { }

/// <summary>
/// Marker for a shard query after a Select projection.
/// </summary>
public interface ISelectShardQueryable<TSource, TResult> : IShardQueryable<TResult> { }
using Shardis.Model;

namespace Shardis.Query;

/// <summary>Targeted execution extensions (fan-out reduction).</summary>
public static class ShardQueryableTargetingExtensions
{
    /// <summary>Target a single shard id.</summary>
    public static IShardQueryable<T> WhereShard<T>(this IShardQueryable<T> source, ShardId id)
    {
        ArgumentNullException.ThrowIfNull(source);
        var model = source.Model.WithTargetShards(new[] { id });
        return new ShardQueryable<T>(source.Executor, model);
    }

    /// <summary>Target a set of shard ids.</summary>
    public static IShardQueryable<T> WhereShard<T>(this IShardQueryable<T> source, params ShardId[] ids)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ids);
        var model = source.Model.WithTargetShards(ids);
        return new ShardQueryable<T>(source.Executor, model);
    }
}
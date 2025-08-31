namespace Shardis.Query;

/// <summary>Consumption helpers (materialization) for shard queries.</summary>
/// <summary>Consumption helpers for shard queries (materialization, not adding new operators).</summary>
public static class ShardQueryableConsumptionExtensions
{
    /// <summary>Execute query model and obtain an async enumerable (unordered).</summary>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IShardQueryable<T> source, CancellationToken ct = default)
        => source.Executor.ExecuteAsync<T>(source.Model, ct);

    /// <summary>Materialize the query to a list (enumerates the unordered merged stream).</summary>
    public static async Task<List<T>> ToListAsync<T>(this IShardQueryable<T> source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var list = new List<T>();

        await foreach (var i in source.ToAsyncEnumerable(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            list.Add(i);
        }

        return list;
    }
}
namespace Shardis.Query;

/// <summary>
/// Terminal (client-side) operations over shard queries. These enumerate the merged stream; prefer future
/// server-side aggregates (not yet implemented) for large data sets when available.
/// </summary>
public static class ShardQueryableTerminalExtensions
{
    /// <summary>Materialize first element or default value.</summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(this IShardQueryable<T> source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await foreach (var item in source.ToAsyncEnumerable(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            return item;
        }
        return default;
    }

    /// <summary>Determine whether the sequence has at least one element (short-circuits).</summary>
    public static async Task<bool> AnyAsync<T>(this IShardQueryable<T> source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await foreach (var _ in source.ToAsyncEnumerable(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    /// <summary>Count elements by enumerating the full merged stream.</summary>
    public static async Task<long> CountAsync<T>(this IShardQueryable<T> source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        long count = 0;
        await foreach (var _ in source.ToAsyncEnumerable(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            count++;
        }
        return count;
    }
}

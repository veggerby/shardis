namespace Shardis.Query.Marten;

/// <summary>
/// Abstraction for turning a provider-specific <see cref="IQueryable{T}"/> into an asynchronous streaming sequence
/// without materializing the full result set in memory.
/// </summary>
public interface IQueryableShardMaterializer
{
    /// <summary>
    /// Convert the given query into an asynchronous stream honoring the cancellation token.
    /// Implementations should prefer provider-native async streaming if available, otherwise fall back to safe pagination.
    /// </summary>
    IAsyncEnumerable<T> ToAsyncEnumerable<T>(IQueryable<T> query, CancellationToken ct) where T : notnull;
}
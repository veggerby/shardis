namespace Shardis.Query.Execution;

/// <summary>
/// Executes composed query models across all shards returning an unordered merged stream.
/// Implementations must honor cancellation and avoid materializing whole result sets unless required by backend semantics.
/// </summary>
public interface IShardQueryExecutor
{
    /// <summary>
    /// Execute the query model across all shards producing a streaming (unordered) async sequence.
    /// Enumeration observes <paramref name="ct"/> and ends early when cancellation is requested. Cooperative model:
    /// the iterator simply stops yielding without throwing an <see cref="OperationCanceledException"/> unless the underlying provider throws.
    /// Downstream consumers relying on explicit exceptions must perform their own token checks if needed.
    /// </summary>
    IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default);

    /// <summary>Optional capabilities descriptor (default: none).</summary>
    IShardQueryCapabilities Capabilities => BasicQueryCapabilities.None;
}
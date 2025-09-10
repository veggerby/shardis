using Shardis.Query.Execution.FailureHandling;

namespace Shardis.Query.Execution;

/// <summary>
/// Executor wrapper applying a shard failure handling strategy over an inner executor that may surface shard exceptions interleaved.
/// Strategy is invoked per underlying error.
/// </summary>
internal sealed class FailureHandlingExecutor : IShardQueryExecutor
{
    private readonly IShardQueryExecutor _inner;
    private readonly IShardQueryFailureStrategy _strategy;

    public FailureHandlingExecutor(IShardQueryExecutor inner, IShardQueryFailureStrategy strategy)
    {
        _inner = inner;
        _strategy = strategy;
    }

    public IShardQueryCapabilities Capabilities => _inner.Capabilities;

    // Failure mode inferred externally by checking wrapper type and underlying strategy instance.

    public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // For now assume inner already merges shards; to apply per-shard failure handling we need inner to surface annotated failures.
        // Interim implementation: treat any exception as from an unspecified shard index = -1.
        await foreach (var item in ExecuteWithFailureHandling<TResult>(model, ct))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<TResult> ExecuteWithFailureHandling<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        IAsyncEnumerator<TResult>? e = null;
        try
        {
            e = _inner.ExecuteAsync<TResult>(model, ct).GetAsyncEnumerator(ct);
            while (true)
            {
                TResult current;
                try
                {
                    if (!await e.MoveNextAsync().ConfigureAwait(false))
                    {
                        yield break;
                    }
                    current = e.Current;
                }
                catch (Exception ex) when (Handle(ex))
                {
                    continue; // skip failed element
                }

                yield return current;
            }
        }
        finally
        {
            if (e is not null)
            {
                await e.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private bool Handle(Exception ex)
    {
        return _strategy.OnShardException(ex, -1);
    }
}
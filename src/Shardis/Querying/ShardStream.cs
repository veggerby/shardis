namespace Shardis.Querying;

internal sealed class ShardStream<TItem> : IAsyncEnumerable<ShardItem<TItem>>
{
    private readonly IShardisEnumerator<TItem> _enumerator;

    public int ShardCount => _enumerator.ShardCount;

    public ShardStream(IShardisEnumerator<TItem> enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator, nameof(enumerator));

        _enumerator = enumerator;
    }

    public ValueTask DisposeAsync() => _enumerator.DisposeAsync();

    public IAsyncEnumerator<ShardItem<TItem>> GetAsyncEnumerator(CancellationToken cancellationToken = default) => _enumerator;
}

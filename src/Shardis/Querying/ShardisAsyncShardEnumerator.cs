
using Shardis.Model;

namespace Shardis.Querying;

internal sealed class ShardisAsyncShardEnumerator<TItem> : IShardisAsyncEnumerator<TItem>
{
    private readonly ShardId _shardId;
    private readonly IAsyncEnumerator<TItem> _enumerator;

    public int ShardCount => 1; // Each ShardStream represents a single shard
    public bool HasValue { get; private set; } = false;
    public bool IsComplete { get; private set; } = false;
    public bool IsPrimed { get; private set; } = false;
    public ShardItem<TItem> Current { get; private set; } = default!;

    public ShardisAsyncShardEnumerator(ShardId shardId, IAsyncEnumerator<TItem> enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator, nameof(enumerator));

        _shardId = shardId;
        _enumerator = enumerator;
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        IsPrimed = true;

        if (await _enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            Current = new(_shardId, _enumerator.Current);
            HasValue = true;
            return true;
        }

        HasValue = false;
        IsComplete = true;
        return false;
    }

    public ValueTask DisposeAsync() => _enumerator.DisposeAsync();
}
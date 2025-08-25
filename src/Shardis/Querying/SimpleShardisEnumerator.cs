using Shardis.Model;

namespace Shardis.Querying;

internal class SimpleShardisAsyncEnumerator<T>(ShardId shardId, IAsyncEnumerable<T> source) : IShardisAsyncEnumerator<T>
{
    private readonly IAsyncEnumerator<T> _inner = source.GetAsyncEnumerator();
    private readonly ShardId _shardId = shardId;

    public int ShardCount => 1;
    public bool IsComplete { get; private set; }
    public bool IsPrimed { get; private set; }
    public bool HasValue { get; private set; }

    public ShardItem<T> Current { get; private set; } = default!;

    public async ValueTask<bool> MoveNextAsync()
    {
        IsPrimed = true;

        if (await _inner.MoveNextAsync())
        {
            Current = new(_shardId, _inner.Current);
            HasValue = true;
            return true;
        }

        IsComplete = true;
        HasValue = false;
        return false;
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
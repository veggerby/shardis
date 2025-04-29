using Shardis.Model;
using Shardis.Querying;

namespace Shardis.Tests.TestHelpers;

public class TestShardisEnumerator<T> : IShardisEnumerator<T>
{
    private readonly IEnumerator<T> _source;
    private readonly List<TimeSpan>? _delays;
    private int _index = 0;

    public ShardId ShardId { get; }
    public int ShardCount => 1;
    public bool IsComplete { get; private set; } = false;
    public bool IsPrimed { get; private set; } = false;
    public bool HasValue { get; private set; } = false;
    public ShardItem<T> Current { get; private set; } = default!;

    public TestShardisEnumerator(IEnumerable<T> items, string shardId, IEnumerable<TimeSpan>? delays = null)
    {
        ArgumentNullException.ThrowIfNull(items, nameof(items));
        ShardId = new ShardId(shardId);
        _source = items.GetEnumerator();
        _delays = delays?.ToList();
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        IsPrimed = true;

        if (_source.MoveNext())
        {
            if (_delays != null && _index < _delays.Count)
            {
                await Task.Delay(_delays[_index]);
            }

            Current = new(ShardId, _source.Current);
            HasValue = true;
            _index++;
            return true;
        }

        HasValue = false;
        IsComplete = true;
        return false;
    }

    public ValueTask DisposeAsync()
    {
        _source.Dispose();
        return ValueTask.CompletedTask;
    }
}
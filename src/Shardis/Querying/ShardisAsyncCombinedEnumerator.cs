using System.Threading.Channels;

using Shardis.Model;

namespace Shardis.Querying;

/// <summary>
/// A streaming enumerator that concurrently merges multiple shard enumerators.
/// Items are yielded as they arrive (unordered), wrapped in <see cref="ShardItem{T}"/>.
/// </summary>
internal sealed class ShardisAsyncCombinedEnumerator<TItem> : IShardisAsyncEnumerator<TItem>
{
    private readonly List<IShardisAsyncEnumerator<TItem>> _shardEnumerators;
    private readonly CancellationToken _cancellationToken;

    private readonly Channel<ShardItem<TItem>> _channel;
    private Task? _backgroundReader;
    private ShardItem<TItem> _current;

    public ShardisAsyncCombinedEnumerator(IEnumerable<IShardisAsyncEnumerator<TItem>> shardEnumerators, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shardEnumerators, nameof(shardEnumerators));

        _shardEnumerators = shardEnumerators.ToList();
        _cancellationToken = cancellationToken;

        _channel = Channel.CreateUnbounded<ShardItem<TItem>>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });
    }

    public ShardId CurrentShardId => _current.ShardId;
    public int ShardCount => _shardEnumerators.Count;
    public bool IsComplete { get; private set; }
    public bool HasValue { get; private set; }
    public bool IsPrimed { get; private set; }

    public ShardItem<TItem> Current => HasValue ? _current : throw new InvalidOperationException("Enumeration not started.");

    public async ValueTask DisposeAsync()
    {
        foreach (var shard in _shardEnumerators)
        {
            await shard.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_backgroundReader == null)
        {
            StartBackgroundDrainer();
        }

        IsPrimed = true;

        while (await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false))
        {
            if (_channel.Reader.TryRead(out var item))
            {
                _current = item;
                HasValue = true;
                return true;
            }
        }

        HasValue = false;
        IsComplete = true;
        return false;
    }

    private void StartBackgroundDrainer()
    {
        _backgroundReader = Task.Run(async () =>
        {
            try
            {
                var tasks = _shardEnumerators.Select(async shard =>
                {
                    while (await shard.MoveNextAsync().ConfigureAwait(false))
                    {
                        await _channel.Writer.WriteAsync(shard.Current, _cancellationToken).ConfigureAwait(false);
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _channel.Writer.TryComplete(ex);
                return;
            }

            _channel.Writer.TryComplete();
        }, _cancellationToken);
    }
}

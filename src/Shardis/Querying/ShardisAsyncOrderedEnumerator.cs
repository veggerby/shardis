using Shardis.Model;

namespace Shardis.Querying;

/// <summary>
/// A globally ordered enumerator that performs a k-way merge across multiple ordered shard streams.
/// </summary>
internal sealed class ShardisAsyncOrderedEnumerator<T, TKey> : IShardisAsyncOrderedEnumerator<T>
    where TKey : IComparable<TKey>
{
    private readonly IList<IShardisAsyncEnumerator<T>> _streams;
    private readonly Func<T, TKey> _keySelector;
    private readonly CancellationToken _cancellationToken;

    private IShardisAsyncEnumerator<T>? _currentStream;

    public ShardisAsyncOrderedEnumerator(
        IEnumerable<IShardisAsyncEnumerator<T>> shardStreams,
        Func<T, TKey> keySelector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shardStreams, nameof(shardStreams));
        ArgumentNullException.ThrowIfNull(keySelector, nameof(keySelector));

        _streams = shardStreams.ToList();
        _keySelector = keySelector;
        _cancellationToken = cancellationToken;
    }

    public int ShardCount => _streams.Count;

    public bool IsComplete { get; private set; } = false;
    public bool HasValue => _currentStream?.HasValue == true;
    public bool IsPrimed => _currentStream?.IsPrimed == true;

    public ShardItem<T> Current => _currentStream is not null ? _currentStream.Current : throw new ShardisException("Enumeration has not started.");
    private bool _readyToAdvance = false;

    public async ValueTask<bool> MoveNextAsync()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Advance previously selected stream only if ready
        if (_readyToAdvance && _currentStream is not null)
        {
            var hasMore = await _currentStream.MoveNextAsync().ConfigureAwait(false);
            if (!hasMore)
            {
                _currentStream = null;
            }
        }

        _readyToAdvance = false;

        // Prime any uninitialized streams
        foreach (var stream in _streams)
        {
            if (!stream.IsPrimed && !stream.IsComplete)
            {
                await stream.MoveNextAsync().ConfigureAwait(false);
            }
        }

        // Select the stream with the smallest item (globally ordered)
        _currentStream = _streams
            .Where(s => s.HasValue)
            .OrderBy(s => _keySelector(s.Current.Item))
            .FirstOrDefault();

        if (_currentStream == null)
        {
            // Still not done: check if any stream can *still* yield data in next round
            bool stillActive = _streams.Any(s => !s.IsComplete);
            IsComplete = !stillActive;
            return false;
        }

        _readyToAdvance = true;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var stream in _streams)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}

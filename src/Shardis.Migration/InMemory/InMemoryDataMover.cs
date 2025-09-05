
using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

namespace Shardis.Migration.InMemory;
/// <summary>
/// Simulated data mover storing copied key markers in-memory. Supports transient and permanent failure injection.
/// </summary>
internal sealed class InMemoryDataMover<TKey> : IShardDataMover<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly HashSet<KeyMove<TKey>> _copied = [];
    private readonly HashSet<KeyMove<TKey>> _verified = [];
    private readonly object _lock = new();

    public Func<KeyMove<TKey>, Exception?>? CopyFailureInjector { get; set; }
    public Func<KeyMove<TKey>, Exception?>? VerifyFailureInjector { get; set; }
    public Func<KeyMove<TKey>, bool>? VerificationMismatchInjector { get; set; }

    public Task CopyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ex = CopyFailureInjector?.Invoke(move);

        if (ex != null)
        {
            throw ex;
        }

        lock (_lock)
        {
            _copied.Add(move);
        }

        return Task.CompletedTask;
    }

    public Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ex = VerifyFailureInjector?.Invoke(move);

        if (ex != null)
        {
            throw ex;
        }

        bool mismatch = VerificationMismatchInjector?.Invoke(move) == true;

        lock (_lock)
        {
            if (!mismatch && _copied.Contains(move))
            {
                _verified.Add(move);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }
}
using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

// Local delegating mover to inject failures without accessing internal test mover types.
internal sealed class FailureInjectingMover(IShardDataMover<string> inner) : IShardDataMover<string>
{
    private readonly IShardDataMover<string> _inner = inner;
    public Func<KeyMove<string>, Exception?>? CopyFailure { get; set; }
    public Task CopyAsync(KeyMove<string> move, CancellationToken ct)
    {
        var ex = CopyFailure?.Invoke(move);
        if (ex != null) throw ex;
        return _inner.CopyAsync(move, ct);
    }
    public Task<bool> VerifyAsync(KeyMove<string> move, CancellationToken ct) => _inner.VerifyAsync(move, ct);
}

namespace Shardis.Query;

/// <summary>
/// Provides public access to the internal unordered streaming merge primitive.
/// Produces an interleaved asynchronous sequence in arrival order (non-deterministic across shards).
/// Optionally specify <c>channelCapacity</c> (argument of <see cref="Merge"/>) to enable bounded backpressure (blocking producers when the buffer is full).
/// </summary>
public static class UnorderedMergeHelper
{
    /// <summary>Create an unordered merged stream interleaving multiple async sources (arrival order).</summary>
    public static IAsyncEnumerable<object> Merge(IEnumerable<IAsyncEnumerable<object>> sources, CancellationToken ct = default, int? channelCapacity = null)
        => Internals.UnorderedMerge.Merge(sources, ct, channelCapacity);
}
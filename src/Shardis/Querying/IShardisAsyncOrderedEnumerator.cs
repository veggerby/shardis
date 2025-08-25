namespace Shardis.Querying;

/// <summary>
/// A globally ordered async enumerator over one or more shards.
/// </summary>
/// <typeparam name="T">The result type yielded by the stream.</typeparam>
public interface IShardisAsyncOrderedEnumerator<T> : IShardisAsyncEnumerator<T>
{
}
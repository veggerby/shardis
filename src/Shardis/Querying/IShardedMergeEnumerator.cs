namespace Shardis.Querying;

/// <summary>
/// An async enumerator that merges multiple ordered shard streams.
/// </summary>
/// <typeparam name="T">The result type yielded by the stream.</typeparam>
public interface IShardedMergeEnumerator<T> : IShardisEnumerator<T>
{
}


using Shardis.Model;

namespace Shardis.Migration.Model;
/// <summary>
/// Represents the planned movement of a single shard key from a source shard to a target shard.
/// </summary>
/// <typeparam name="TKey">The underlying key value type.</typeparam>
/// <param name="Key">The shard key to migrate.</param>
/// <param name="Source">The source shard currently owning the key.</param>
/// <param name="Target">The target shard to which the key will be moved.</param>
public readonly record struct KeyMove<TKey>(ShardKey<TKey> Key, ShardId Source, ShardId Target)
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Returns a readable string for diagnostics (does not include raw key value beyond ToString()).</summary>
    public override string ToString() => $"{Key} {Source.Value}->{Target.Value}";
}
namespace Shardis.Migration.EntityFrameworkCore;

/// <summary>
/// Minimal entity accessor used by migration to read key and optional rowversion without coupling to domain models.
/// </summary>
/// <typeparam name="TKey">Underlying key type.</typeparam>
public interface IShardEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Primary key value.</summary>
    TKey Key { get; }

    /// <summary>Optional concurrency / rowversion property (nullable when not supported).</summary>
    byte[]? RowVersion { get; }
}
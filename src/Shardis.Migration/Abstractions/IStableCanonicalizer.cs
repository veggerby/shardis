namespace Shardis.Migration.Abstractions;

/// <summary>Provides stable canonical serialization for hashing / verification.</summary>
public interface IStableCanonicalizer
{
    /// <summary>Produces a deterministic UTF8 byte representation for the provided value.</summary>
    byte[] ToCanonicalUtf8(object value);
}

namespace Shardis.Model;

/// <summary>
/// Represents a simple shard with a connection string.
/// </summary>
public interface ISimpleShard : IShard<string>
{
    /// <summary>
    /// Gets the connection string for the shard.
    /// </summary>
    string ConnectionString { get; }
}
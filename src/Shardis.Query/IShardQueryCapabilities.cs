namespace Shardis.Query;

/// <summary>Describes optional features supported by a concrete query executor.</summary>
public interface IShardQueryCapabilities
{
    /// <summary>True if server-side ordering (OrderBy/ThenBy) is supported.</summary>
    bool SupportsOrdering { get; }
    /// <summary>True if pagination (Skip/Take) is supported.</summary>
    bool SupportsPagination { get; }
}
using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Introspection helpers for producing a lightweight textual plan for a composed shard query.
/// </summary>
public static class ShardQueryExplainExtensions
{
    /// <summary>
    /// Produce a simple plan object describing the current query model (where predicate count, projection presence, source type, capabilities).
    /// </summary>
    public static ShardQueryPlan Explain<T>(this IShardQueryable<T> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var model = query.Model;
        var caps = query.Executor.Capabilities;
        return new ShardQueryPlan(model.SourceType.FullName ?? model.SourceType.Name,
                                  model.Where.Count,
                                  model.Select is not null,
                                  caps.SupportsOrdering,
                                  caps.SupportsPagination);
    }
}

/// <summary>
/// Lightweight query plan description (stable for diagnostics / logging, not an execution contract).
/// </summary>
/// <param name="Source">Source element type.</param>
/// <param name="PredicateCount">Number of where predicates.</param>
/// <param name="HasProjection">True if a projection has been applied.</param>
/// <param name="SupportsOrdering">Executor ordering capability flag.</param>
/// <param name="SupportsPagination">Executor pagination capability flag.</param>
public readonly record struct ShardQueryPlan(string Source,
                                              int PredicateCount,
                                              bool HasProjection,
                                              bool SupportsOrdering,
                                              bool SupportsPagination);
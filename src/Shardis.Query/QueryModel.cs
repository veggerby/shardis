using System.Linq.Expressions;

namespace Shardis.Query;

/// <summary>
/// Immutable representation of a composed query (Where predicates and optional projection).
/// </summary>
public sealed class QueryModel
{
    /// <summary>Root element type (entity) the query originates from.</summary>
    public Type SourceType { get; }
    /// <summary>Ordered sequence of predicate lambdas applied per shard.</summary>
    public IReadOnlyList<LambdaExpression> Where { get; }
    /// <summary>Optional final projection (single projection supported).</summary>
    public LambdaExpression? Select { get; }
    /// <summary>Optional explicit target shard set; when null all shards are targeted.</summary>
    public IReadOnlyList<Shardis.Model.ShardId>? TargetShards { get; }

    private QueryModel(Type sourceType, IReadOnlyList<LambdaExpression> where, LambdaExpression? select, IReadOnlyList<Shardis.Model.ShardId>? targetShards)
    {
        SourceType = sourceType;
        Where = where;
        Select = select;
        TargetShards = targetShards;
    }

    /// <summary>Create a new root model for <paramref name="sourceType"/>.</summary>
    public static QueryModel Create(Type sourceType)
        => new(sourceType, Array.Empty<LambdaExpression>(), null, null);

    /// <summary>Return a new model with an appended predicate (immutably copies existing predicates).</summary>
    public QueryModel WithWhere(LambdaExpression predicate)
        => new(SourceType, Where.Concat(new[] { predicate }).ToArray(), Select, TargetShards);

    /// <summary>Return a new model with a projection (replaces any prior projection).</summary>
    public QueryModel WithSelect(LambdaExpression projector)
        => new(SourceType, Where, projector, TargetShards);

    /// <summary>Return a new model targeting only the supplied shard ids (null restores fan-out to all).</summary>
    public QueryModel WithTargetShards(IReadOnlyList<Shardis.Model.ShardId>? ids)
        => new(SourceType, Where, Select, ids);
}
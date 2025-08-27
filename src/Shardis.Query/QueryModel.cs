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

    private QueryModel(Type sourceType, IReadOnlyList<LambdaExpression> where, LambdaExpression? select)
    {
        SourceType = sourceType;
        Where = where;
        Select = select;
    }

    /// <summary>Create a new root model for <paramref name="sourceType"/>.</summary>
    public static QueryModel Create(Type sourceType)
        => new(sourceType, Array.Empty<LambdaExpression>(), null);

    /// <summary>Return a new model with an appended predicate (immutably copies existing predicates).</summary>
    public QueryModel WithWhere(LambdaExpression predicate)
        => new(SourceType, Where.Concat(new[] { predicate }).ToArray(), Select);

    /// <summary>Return a new model with a projection (replaces any prior projection).</summary>
    public QueryModel WithSelect(LambdaExpression projector)
        => new(SourceType, Where, projector);
}
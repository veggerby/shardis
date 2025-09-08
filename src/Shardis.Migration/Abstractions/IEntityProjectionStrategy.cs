namespace Shardis.Migration.Abstractions;

/// <summary>
/// Provides value transformation / shape projection during migration to accommodate schema evolution.
/// Providers may override this to map source entity representations to target representations.
/// </summary>
public interface IEntityProjectionStrategy
{
    /// <summary>Projects a source object to a target object, potentially transforming schema or version.</summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TTarget">Target type.</typeparam>
    /// <param name="source">The source instance (non-null).</param>
    /// <param name="context">Projection context (schema version metadata).</param>
    /// <returns>Projected target instance.</returns>
    TTarget Project<TSource, TTarget>(TSource source, ProjectionContext context)
        where TSource : class
        where TTarget : class;
}

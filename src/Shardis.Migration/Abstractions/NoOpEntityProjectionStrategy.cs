namespace Shardis.Migration.Abstractions;

/// <summary>No-op projection implementation returning the source when types match.</summary>
public sealed class NoOpEntityProjectionStrategy : IEntityProjectionStrategy
{
    /// <summary>Singleton instance to avoid repeated allocations.</summary>
    public static readonly NoOpEntityProjectionStrategy Instance = new();

    /// <summary>Projects the source to the target by identity when types are identical; otherwise throws.</summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TTarget">Target type.</typeparam>
    /// <param name="source">Source instance (non-null).</param>
    /// <param name="context">Projection context.</param>
    /// <returns>The same instance cast to <typeparamref name="TTarget"/> if compatible.</returns>
    /// <exception cref="InvalidOperationException">Thrown if source/target types differ.</exception>
    public TTarget Project<TSource, TTarget>(TSource source, ProjectionContext context)
        where TSource : class
        where TTarget : class
    {
        if (source is TTarget t)
        {
            return t;
        }

        throw new InvalidOperationException("No-op projection cannot map differing source/target types. Provide a custom strategy.");
    }
}
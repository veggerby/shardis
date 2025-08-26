namespace Shardis.Migration.Abstractions;

using Shardis.Migration.Model;

/// <summary>
/// Produces a deterministic migration plan from a source topology to a target topology.
/// </summary>
/// <typeparam name="TKey">Underlying key value type.</typeparam>
public interface IShardMigrationPlanner<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Creates a migration plan describing the movements required to transform the <paramref name="from"/> topology into <paramref name="to"/>.
    /// </summary>
    /// <param name="from">The current topology snapshot.</param>
    /// <param name="to">The desired topology snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The constructed migration plan.</returns>
    Task<MigrationPlan<TKey>> CreatePlanAsync(TopologySnapshot<TKey> from, TopologySnapshot<TKey> to, CancellationToken ct);
}
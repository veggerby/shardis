namespace Shardis.Migration.Model;

using System.Linq;

/// <summary>
/// Represents an immutable plan for migrating a set of shard keys.
/// </summary>
/// <typeparam name="TKey">The underlying key value type.</typeparam>
public sealed record MigrationPlan<TKey>
	where TKey : notnull, IEquatable<TKey>
{
	/// <summary>Gets the unique identifier of the migration plan.</summary>
	public Guid PlanId { get; }

	/// <summary>Gets the creation timestamp in UTC.</summary>
	public DateTimeOffset CreatedAtUtc { get; }

	/// <summary>Gets the ordered list of key moves composing the plan.</summary>
	public IReadOnlyList<KeyMove<TKey>> Moves { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationPlan{TKey}"/> class.
	/// Performs a defensive copy of the provided moves to preserve immutability/determinism.
	/// </summary>
	/// <param name="planId">The plan identifier.</param>
	/// <param name="createdAtUtc">The creation timestamp (UTC).</param>
	/// <param name="moves">The sequence of key moves (will be enumerated once and copied).</param>
	public MigrationPlan(Guid planId, DateTimeOffset createdAtUtc, IEnumerable<KeyMove<TKey>> moves)
	{
		PlanId = planId;
		CreatedAtUtc = createdAtUtc;
		var materialized = moves as KeyMove<TKey>[] ?? moves.ToArray();
		Moves = materialized.Length == 0 ? Array.Empty<KeyMove<TKey>>() : materialized;
	}
}

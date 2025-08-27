namespace Shardis.Migration.Execution;

/// <summary>Summary of a migration execution.</summary>
/// <param name="PlanId">Plan identifier.</param>
/// <param name="Planned">Total planned key moves.</param>
/// <param name="Done">Number of successfully migrated keys.</param>
/// <param name="Failed">Number of permanently failed keys.</param>
/// <param name="Elapsed">Total elapsed wall-clock time.</param>
public sealed record MigrationSummary(Guid PlanId, int Planned, int Done, int Failed, TimeSpan Elapsed);
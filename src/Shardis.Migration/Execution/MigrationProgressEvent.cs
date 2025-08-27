namespace Shardis.Migration.Execution;

/// <summary>
/// Represents a periodic snapshot of migration progress suitable for UI/metrics.
/// All counts are monotonic except ActiveCopy / ActiveVerify which are gauges.
/// </summary>
/// <param name="PlanId">Migration plan identifier.</param>
/// <param name="Total">Total planned key moves.</param>
/// <param name="Copied">Number of keys whose data has been copied.</param>
/// <param name="Verified">Number of keys successfully verified.</param>
/// <param name="Swapped">Number of keys whose ownership has been swapped.</param>
/// <param name="Failed">Number of keys permanently failed.</param>
/// <param name="ActiveCopy">Current number of in-flight copy operations.</param>
/// <param name="ActiveVerify">Current number of in-flight verify operations.</param>
/// <param name="TimestampUtc">UTC timestamp of the snapshot.</param>
public sealed record MigrationProgressEvent(
    Guid PlanId,
    int Total,
    int Copied,
    int Verified,
    int Swapped,
    int Failed,
    int ActiveCopy,
    int ActiveVerify,
    DateTimeOffset TimestampUtc);
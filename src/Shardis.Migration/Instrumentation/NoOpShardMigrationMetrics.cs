
using Shardis.Migration.Abstractions;

namespace Shardis.Migration.Instrumentation;
/// <summary>
/// No-op implementation of <see cref="IShardMigrationMetrics"/> used when metrics collection is disabled.
/// </summary>
internal sealed class NoOpShardMigrationMetrics : IShardMigrationMetrics
{
    public void IncPlanned(long delta = 1) { }
    public void IncCopied(long delta = 1) { }
    public void IncVerified(long delta = 1) { }
    public void IncSwapped(long delta = 1) { }
    public void IncFailed(long delta = 1) { }
    public void IncRetries(long delta = 1) { }
    public void SetActiveCopy(int value) { }
    public void SetActiveVerify(int value) { }
}
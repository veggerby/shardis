using Shardis.Migration.Abstractions;

namespace Shardis.Migration.Durable.Sample;

// Simple in-memory metrics to observe real counts for the durable EF sample.
public sealed class CountingMetrics : IShardMigrationMetrics
{
    public long Planned; public long Copied; public long Verified; public long Swapped; public long Failed; public long Retries;
    public int ActiveCopy; public int ActiveVerify;
    public double CopyMs; public double VerifyMs; public double SwapBatchMs; public double TotalMs;

    public void IncPlanned(long delta = 1) => Interlocked.Add(ref Planned, delta);
    public void IncCopied(long delta = 1) => Interlocked.Add(ref Copied, delta);
    public void IncVerified(long delta = 1) => Interlocked.Add(ref Verified, delta);
    public void IncSwapped(long delta = 1) => Interlocked.Add(ref Swapped, delta);
    public void IncFailed(long delta = 1) => Interlocked.Add(ref Failed, delta);
    public void IncRetries(long delta = 1) => Interlocked.Add(ref Retries, delta);
    public void SetActiveCopy(int value) => ActiveCopy = value;
    public void SetActiveVerify(int value) => ActiveVerify = value;
    public void ObserveCopyDuration(double ms) => CopyMs += ms;
    public void ObserveVerifyDuration(double ms) => VerifyMs += ms;
    public void ObserveSwapBatchDuration(double ms) => SwapBatchMs += ms;
    public void ObserveTotalElapsed(double ms) => TotalMs = ms;
}
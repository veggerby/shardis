using Shardis.Migration.Abstractions;

namespace Shardis.Migration.Instrumentation;
/// <summary>
/// Lightweight in-process metrics collector using atomic counters. Intended for tests / samples.
/// </summary>
internal sealed class SimpleShardMigrationMetrics : IShardMigrationMetrics
{
    private long _planned;
    private long _copied;
    private long _verified;
    private long _swapped;
    private long _failed;
    private long _retries;
    private int _activeCopy;
    private int _activeVerify;
    private double _copyDurationTotalMs;
    private long _copyDurationCount;
    private double _verifyDurationTotalMs;
    private long _verifyDurationCount;
    private double _swapBatchDurationTotalMs;
    private long _swapBatchDurationCount;
    private double _totalElapsedMs;

    public void IncPlanned(long delta = 1) => Interlocked.Add(ref _planned, delta);
    public void IncCopied(long delta = 1) => Interlocked.Add(ref _copied, delta);
    public void IncVerified(long delta = 1) => Interlocked.Add(ref _verified, delta);
    public void IncSwapped(long delta = 1) => Interlocked.Add(ref _swapped, delta);
    public void IncFailed(long delta = 1) => Interlocked.Add(ref _failed, delta);
    public void IncRetries(long delta = 1) => Interlocked.Add(ref _retries, delta);
    public void SetActiveCopy(int value) => Interlocked.Exchange(ref _activeCopy, value);
    public void SetActiveVerify(int value) => Interlocked.Exchange(ref _activeVerify, value);
    public void ObserveCopyDuration(double ms) { Interlocked.Add(ref _copyDurationCount, 1); Add(ref _copyDurationTotalMs, ms); }
    public void ObserveVerifyDuration(double ms) { Interlocked.Add(ref _verifyDurationCount, 1); Add(ref _verifyDurationTotalMs, ms); }
    public void ObserveSwapBatchDuration(double ms) { Interlocked.Add(ref _swapBatchDurationCount, 1); Add(ref _swapBatchDurationTotalMs, ms); }
    public void ObserveTotalElapsed(double ms) { Interlocked.Exchange(ref _totalElapsedMs, ms); }

    // Expose snapshot for tests / diagnostics
    public (long planned, long copied, long verified, long swapped, long failed, long retries, int activeCopy, int activeVerify, double avgCopyMs, double avgVerifyMs, double avgSwapBatchMs, double totalElapsedMs) Snapshot()
    {
        var copyCount = Interlocked.Read(ref _copyDurationCount);
        var verifyCount = Interlocked.Read(ref _verifyDurationCount);
        var swapCount = Interlocked.Read(ref _swapBatchDurationCount);
        return (
            Interlocked.Read(ref _planned),
            Interlocked.Read(ref _copied),
            Interlocked.Read(ref _verified),
            Interlocked.Read(ref _swapped),
            Interlocked.Read(ref _failed),
            Interlocked.Read(ref _retries),
            Volatile.Read(ref _activeCopy),
            Volatile.Read(ref _activeVerify),
            copyCount == 0 ? 0 : Volatile.Read(ref _copyDurationTotalMs) / copyCount,
            verifyCount == 0 ? 0 : Volatile.Read(ref _verifyDurationTotalMs) / verifyCount,
            swapCount == 0 ? 0 : Volatile.Read(ref _swapBatchDurationTotalMs) / swapCount,
            Volatile.Read(ref _totalElapsedMs));
    }

    private static void Add(ref double location, double value)
    {
        double initial, computed;
        do
        {
            initial = Volatile.Read(ref location);
            computed = initial + value;
        } while (Math.Abs(Interlocked.CompareExchange(ref location, computed, initial) - initial) > double.Epsilon);
    }
}

using System.Threading;

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

    public void IncPlanned(long delta = 1) => Interlocked.Add(ref _planned, delta);
    public void IncCopied(long delta = 1) => Interlocked.Add(ref _copied, delta);
    public void IncVerified(long delta = 1) => Interlocked.Add(ref _verified, delta);
    public void IncSwapped(long delta = 1) => Interlocked.Add(ref _swapped, delta);
    public void IncFailed(long delta = 1) => Interlocked.Add(ref _failed, delta);
    public void IncRetries(long delta = 1) => Interlocked.Add(ref _retries, delta);
    public void SetActiveCopy(int value) => Interlocked.Exchange(ref _activeCopy, value);
    public void SetActiveVerify(int value) => Interlocked.Exchange(ref _activeVerify, value);

    // Expose snapshot for tests / diagnostics
    public (long planned, long copied, long verified, long swapped, long failed, long retries, int activeCopy, int activeVerify) Snapshot()
        => (Interlocked.Read(ref _planned), Interlocked.Read(ref _copied), Interlocked.Read(ref _verified), Interlocked.Read(ref _swapped), Interlocked.Read(ref _failed), Interlocked.Read(ref _retries), Volatile.Read(ref _activeCopy), Volatile.Read(ref _activeVerify));
}
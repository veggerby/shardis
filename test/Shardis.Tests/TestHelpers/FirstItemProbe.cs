using System.Diagnostics;

namespace Shardis.Tests;

public sealed class FirstItemProbe
{
    private Stopwatch? _sw;
    public long Us { get; private set; } = -1;
    public void Start() => _sw = Stopwatch.StartNew();
    public void Hit()
    {
        if (Us >= 0 || _sw is null) return;
        Us = (long)(_sw.ElapsedTicks * (1_000_000.0 / Stopwatch.Frequency));
    }
}
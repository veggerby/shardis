namespace Shardis.Tests.TestInfra;

public sealed class LeakProbe
{
    private readonly List<WeakReference> _refs = new();
    public void Track(object o) => _refs.Add(new WeakReference(o));
    public void ForceGC()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    public bool AllCollected() => _refs.All(r => r.Target is null);
}
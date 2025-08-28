namespace Shardis.Querying;

internal sealed class ObserverMergeProbe(IMergeObserver observer, int sampleEvery = 1) : IOrderedMergeProbe
{
    private readonly IMergeObserver _observer = observer;
    private readonly int _sampleEvery = sampleEvery < 1 ? 1 : sampleEvery;
    private int _counter = 0;

    public void OnHeapSize(int size)
    {
        if ((++_counter % _sampleEvery) != 0)
        {
            return;
        }
        try
        {
            _observer.OnHeapSizeSample(size);
        }
        catch
        {
            // swallow
        }
    }
}
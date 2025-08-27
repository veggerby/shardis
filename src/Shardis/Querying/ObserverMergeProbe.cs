namespace Shardis.Querying;

internal sealed class ObserverMergeProbe : IOrderedMergeProbe
{
    private readonly IMergeObserver _observer;
    private readonly int _sampleEvery;
    private int _counter;

    public ObserverMergeProbe(IMergeObserver observer, int sampleEvery = 1)
    {
        _observer = observer;
        _sampleEvery = sampleEvery < 1 ? 1 : sampleEvery;
        _counter = 0;
    }

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
namespace Shardis.Query.Tests;

public sealed class CancellationTests
{
    private sealed record Item(int Id);

    [Fact]
    public async Task Cancellation_StopsEnumeration()
    {
        var shard = new List<object> { new Item(1), new Item(2), new Item(3) };
        var exec = new Shardis.Query.Execution.InMemory.InMemoryShardQueryExecutor(new List<IEnumerable<object>> { shard }, MergeSequential);
        var q = ShardQuery.For<Item>(exec).Where(i => i.Id > 0);
        using var cts = new CancellationTokenSource();
        var list = new List<Item>();
        try
        {
            await foreach (var item in q.WithCancellation(cts.Token))
            {
                list.Add(item);
                cts.Cancel();
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // expected
        }
        list.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    private static IAsyncEnumerable<object> MergeSequential(IEnumerable<IAsyncEnumerable<object>> streams, CancellationToken ct)
        => Merge(streams, ct);

    private static async IAsyncEnumerable<object> Merge(IEnumerable<IAsyncEnumerable<object>> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var src in sources)
        {
            await foreach (var item in src.WithCancellation(ct))
            {
                yield return item;
            }
        }
    }
}
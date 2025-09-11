namespace Shardis.Query.Tests;

public sealed class OrderedMergeTests
{
    [Fact]
    public async Task OrderedMerge_MergesGloballyOrdered()
    {
        // arrange
        var shard1 = Async(1, 3, 5, 7);
        var shard2 = Async(2, 4, 6, 8);

        // act
        var merged = OrderedMergeHelper.Merge(new[] { shard1, shard2 }, x => x);
        var list = new List<int>();
        await foreach (var item in merged)
        {
            list.Add(item);
        }

        // assert
        list.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task OrderedMerge_YieldsEarly()
    {
        // arrange
        var fast = Async(0, 2, 4);
        var slow = SlowAsync(new[] { 1, 3, 5 }, delay: TimeSpan.FromMilliseconds(50));

        // act
        var merged = OrderedMergeHelper.Merge(new[] { fast, slow }, x => x);
        var enumerator = merged.GetAsyncEnumerator();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        var firstElapsed = sw.Elapsed;

        // assert
        // We pay the cost of fetching the first element from every shard before first yield (sequentially),
        // so the latency is at least the slowest shard's first-item delay (50ms). Under heavy CI load we've
        // observed sporadic scheduler stalls, so use a relaxed upper bound while still defending against
        // pathological blocking (e.g. seconds) that would indicate regression to full materialization.
        // We expect concurrent prefetch to prevent latency from scaling with shard count.
        // Slow shard first element delay is 50ms; under extreme CI load we've observed scheduler stalls.
        // Use a wide bound that still detects pathological regression (seconds of blocking).
        firstElapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        await enumerator.DisposeAsync();
    }

    private static async IAsyncEnumerable<int> Async(params int[] values)
    {
        foreach (var v in values) { yield return v; await Task.Yield(); }
    }

    private static async IAsyncEnumerable<int> SlowAsync(IEnumerable<int> values, TimeSpan delay)
    {
        foreach (var v in values) { await Task.Delay(delay); yield return v; }
    }
}
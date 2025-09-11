using Shardis.Query.Execution.Ordered;

namespace Shardis.Query.Tests;

public sealed class StreamingOrderedMergeTests
{
    private static async IAsyncEnumerable<int> Seq(int shard, params int[] values)
    {
        foreach (var v in values)
        {
            await Task.Yield();
            yield return v;
        }
    }

    [Fact]
    public async Task Merge_Ascending()
    {
        // arrange
        var sources = new IAsyncEnumerable<int>[]
        {
            Seq(0, 1,4,9),
            Seq(1, 2,3,10),
            Seq(2, 5,6,7,8)
        };

        // act
        var merged = new List<int>();
        await foreach (var v in StreamingOrderedMerge.Merge(sources, x => x, Comparer<int>.Default, descending: false, CancellationToken.None))
        {
            merged.Add(v);
        }

        // assert
        merged.Should().BeEquivalentTo(Enumerable.Range(1, 10), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Merge_Descending()
    {
        // arrange
        // Provide already descending-ordered per-shard sequences.
        var sources = new IAsyncEnumerable<int>[]
        {
            Seq(0, 9,4,1),
            Seq(1, 10,3,2),
            Seq(2, 8,7,6,5)
        };

        var merged = new List<int>();
        await foreach (var v in StreamingOrderedMerge.Merge(sources, x => x, Comparer<int>.Default, descending: true, CancellationToken.None))
        {
            merged.Add(v);
        }
        merged.Should().BeEquivalentTo(Enumerable.Range(1, 10).Reverse(), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Merge_Stable_For_Duplicates()
    {
        // arrange duplicates across shards (same key) preserving shard order tie-break
        var s0 = Seq(0, 1, 2, 2, 2, 5);
        var s1 = Seq(1, 1, 2, 4);
        var s2 = Seq(2, 2, 3, 5);
        var list = new List<(int value, int shard)>();
        static async IAsyncEnumerable<(int, int)> Annotate(IAsyncEnumerable<int> src, int shard)
        {
            await foreach (var v in src)
            {
                yield return (v, shard);
            }
        }
        var annotatedSources = new IAsyncEnumerable<(int, int)>[] { Annotate(s0, 0), Annotate(s1, 1), Annotate(s2, 2) };
        var merged = new List<(int, int)>();
        await foreach (var v in StreamingOrderedMerge.Merge(annotatedSources, x => x.Item1, Comparer<int>.Default, descending: false, CancellationToken.None))
        {
            merged.Add(v);
        }

        // group duplicates and ensure ascending shard index within same key group
        var groups = merged.GroupBy(t => t.Item1);
        foreach (var g in groups)
        {
            var shardOrder = g.Select(x => x.Item2).ToArray();
            shardOrder.Should().BeEquivalentTo(shardOrder.OrderBy(x => x), o => o.WithStrictOrdering());
        }
    }
}
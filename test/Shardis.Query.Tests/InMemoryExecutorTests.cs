using Shardis.Query.InMemory.Execution;

namespace Shardis.Query.Tests;

public sealed class InMemoryExecutorTests
{
    private sealed record Person(int Id, string Name, int Age);

    [Fact]
    public async Task InMemoryExecutor_AppliesWhereSelect_WithClosures()
    {
        // arrange
        var shard1 = new List<object> { new Person(1, "Alice", 30), new Person(2, "Bob", 25) };
        var shard2 = new List<object> { new Person(3, "Carol", 40) };
        var shard3 = new List<object> { new Person(4, "Dave", 35), new Person(5, "Eve", 22) };
        var shards = new List<IEnumerable<object>> { shard1, shard2, shard3 };
        var exec = new InMemoryShardQueryExecutor(shards, Merge);

        var minAge = 30;
        var q = ShardQuery.For<Person>(exec)
                          .Where(p => p.Age >= minAge)
                          .Select(p => p.Name);

        // act
        var list = await q.ToListAsync();

        // assert
        list.Should().BeEquivalentTo(new[] { "Alice", "Carol", "Dave" });
    }

    private static IAsyncEnumerable<object> Merge(IEnumerable<IAsyncEnumerable<object>> streams, CancellationToken ct)
        => MergeSequential(streams, ct);

    private static async IAsyncEnumerable<object> MergeSequential(IEnumerable<IAsyncEnumerable<object>> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
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
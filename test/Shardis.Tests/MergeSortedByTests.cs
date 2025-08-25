using AwesomeAssertions;

using Shardis.Querying;

namespace Shardis.Tests;

public class MergeSortedByTests
{
    private record User(string Id, DateTime LastLogin);

    private static async IAsyncEnumerable<User> SimulatedShardResults(
        string shardId,
        List<(string Id, int OffsetSeconds, int SimulatedDelayMs)>
            entries)
    {
        foreach (var (id, offset, delayMs) in entries)
        {
            await Task.Delay(delayMs); // simulate delay unique to shard/item
            yield return new User($"{shardId}-{id}", DateTime.UtcNow.AddSeconds(offset));
        }
    }

    [Fact]
    public async Task MergeSortedBy_ShouldReturnGloballyOrderedResults_EvenWithShardDelays()
    {
        // arrange
        var shard1 = SimulatedShardResults("shard1",
        [
            ("a", 10, 100),
            ("c", 30, 300)
        ]);

        var shard2 = SimulatedShardResults("shard2",
        [
            ("b", 20, 800),
            ("d", 40, 100)
        ]);

        var shard3 = SimulatedShardResults("shard3",
        [
            ("e", 25, 200),
            ("f", 50, 1000)
        ]);

        var merged = new List<User>();

        // act
        await foreach (var user in new[] { shard1, shard2, shard3 }.MergeSortedBy(u => u.LastLogin))
        {
            merged.Add(user);
        }

        // assert
        var logins = merged.Select(u => u.LastLogin).ToList();
        for (int i = 1; i < logins.Count; i++)
        {
            (logins[i - 1] <= logins[i]).ShouldBeTrue();
        }
        merged.Select(u => u.Id).ShouldContainInOrder(
            "shard1-a", "shard2-b", "shard3-e", "shard1-c", "shard2-d", "shard3-f");
    }
}
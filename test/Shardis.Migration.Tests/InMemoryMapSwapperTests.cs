using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class InMemoryMapSwapperTests
{
    private static KeyMove<string> M(string key, string from, string to) => new(new ShardKey<string>(key), new(from), new(to));

    [Fact]
    public async Task Swap_Assigns_Target_Shards()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var swapper = new InMemoryMapSwapper<string>(store);
        var batch = new[] { M("k1", "s1", "s2"), M("k2", "s1", "s3") };

        // act
        await swapper.SwapAsync(batch, CancellationToken.None);

        // assert
        store.TryGetShardIdForKey(new("k1"), out var s1).Should().BeTrue();
        store.TryGetShardIdForKey(new("k2"), out var s2).Should().BeTrue();
        s1.Should().Be(new ShardId("s2"));
        s2.Should().Be(new ShardId("s3"));
    }

    [Fact]
    public async Task Swap_PartialFailure_Throws_After_Half()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var swapper = new InMemoryMapSwapper<string>(store) { SimulatePartialFailure = true };
        var batch = new[] { M("k1", "s1", "s2"), M("k2", "s1", "s3"), M("k3", "s1", "s4") };

        // act
        var ex = await Record.ExceptionAsync(() => swapper.SwapAsync(batch, CancellationToken.None));
        ex.Should().BeOfType<InvalidOperationException>();

        // assert
        // At least first half applied (floor(count/2))
        store.TryGetShardIdForKey(new("k1"), out var shard).Should().BeTrue();
        shard.Should().Be(new ShardId("s2"));
    }
}
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Tests;

public class InMemoryShardMapStoreTests
{
    [Fact]
    public void TryGetOrAdd_First_Adds()
    {
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("k1");
        var added = store.TryGetOrAdd(key, () => new ShardId("0"), out var map);
        added.Should().BeTrue();
        map.ShardId.Value.Should().Be("0");
    }

    [Fact]
    public void TryGetOrAdd_Second_ReturnsExisting()
    {
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("k1");
        store.TryGetOrAdd(key, () => new ShardId("0"), out _);
        var added = store.TryGetOrAdd(key, () => new ShardId("1"), out var map2);
        added.Should().BeFalse();
        map2.ShardId.Value.Should().Be("0");
    }

    [Fact]
    public void TryAssignShardToKey_Race_Winner_Stable()
    {
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("r1");
        var ids = new[] { "0", "1", "2", "3" };
        Parallel.ForEach(ids, id => store.TryAssignShardToKey(key, new ShardId(id), out _));
        store.TryGetShardIdForKey(key, out var final).Should().BeTrue();
        final.Value.Should().BeOneOf(ids); // deterministic winner not enforced; existence required
    }

    [Fact]
    public async Task EnumerateAsync_Yields_All_Assignments()
    {
        var store = new InMemoryShardMapStore<string>();
        for (int i = 0; i < 10; i++) store.AssignShardToKey(new ShardKey<string>("k" + i), new ShardId("s" + (i % 2)));
        var list = new List<ShardMap<string>>();
        await foreach (var m in store.EnumerateAsync()) { list.Add(m); }
        list.Count.Should().Be(10);
    }
}
using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class MigrationPlannerTests
{
    private static TopologySnapshot<string> Snapshot(params (string key, string shard)[] assignments)
    {
        var dict = assignments.ToDictionary(a => new ShardKey<string>(a.key), a => new ShardId(a.shard));
        return new TopologySnapshot<string>(dict);
    }

    [Fact]
    public async Task Planner_Adds_Move_For_Reassigned_Key()
    {
        // arrange
        var planner = new InMemoryMigrationPlanner<string>();
        var from = Snapshot(("k1", "s1"), ("k2", "s1"));
        var to = Snapshot(("k1", "s2"), ("k2", "s1"));

        // act
        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);

        // assert
        plan.Moves.Count.Should().Be(1);
        plan.Moves[0].Key.Value.Should().Be("k1");
        plan.Moves[0].Source.Should().Be(new ShardId("s1"));
        plan.Moves[0].Target.Should().Be(new ShardId("s2"));
    }

    [Fact]
    public async Task Planner_Produces_Deterministic_Order()
    {
        // arrange
        var planner = new InMemoryMigrationPlanner<string>();
        var from = Snapshot(("a", "s1"), ("b", "s2"), ("c", "s1"));
        var to = Snapshot(("a", "s2"), ("b", "s1"), ("c", "s2")); // all three move

        // act
        var plan1 = await planner.CreatePlanAsync(from, to, CancellationToken.None);
        var plan2 = await planner.CreatePlanAsync(from, to, CancellationToken.None);

        // assert
        var seq1 = plan1.Moves.Select(m => m.ToString()).ToArray();
        var seq2 = plan2.Moves.Select(m => m.ToString()).ToArray();
        Assert.True(seq1.SequenceEqual(seq2));
    }

    [Fact]
    public async Task Planner_No_Moves_When_Topologies_Identical()
    {
        // arrange
        var planner = new InMemoryMigrationPlanner<string>();
        var from = Snapshot(("k1", "s1"), ("k2", "s2"));
        var to = Snapshot(("k1", "s1"), ("k2", "s2"));

        // act
        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);

        // assert
        plan.Moves.Count.Should().Be(0);
    }

    [Fact]
    public async Task Planner_Ignores_Removed_Keys()
    {
        // arrange
        var planner = new InMemoryMigrationPlanner<string>();
        var from = Snapshot(("k1", "s1"), ("k2", "s1"));
        var to = Snapshot(("k1", "s2")); // k2 removed

        // act
        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);

        // assert
        plan.Moves.Count.Should().Be(1); // only k1 reassigned
        plan.Moves[0].Key.Value.Should().Be("k1");
    }

    [Fact]
    public async Task Planner_Order_Matches_Source_Target_Hash()
    {
        // arrange
        var planner = new InMemoryMigrationPlanner<string>();
        var from = Snapshot(("x1", "s1"), ("x2", "s2"), ("x3", "s1"), ("x4", "s2"));
        var to = Snapshot(("x1", "s2"), ("x2", "s1"), ("x3", "s2"), ("x4", "s1")); // all move

        // act
        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);

        // assert
        // Reconstruct expected ordering using same hash logic: Source, then Target, then StableKeyHash(key)
        static ulong StableKeyHash(ShardKey<string> key)
        {
            var str = key.Value ?? string.Empty;
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return BitConverter.ToUInt64(hash, 0);
        }
        var expected = plan.Moves
            .OrderBy(m => m.Source.Value, StringComparer.Ordinal)
            .ThenBy(m => m.Target.Value, StringComparer.Ordinal)
            .ThenBy(m => StableKeyHash(m.Key))
            .Select(m => m.ToString())
            .ToArray();
        var actual = plan.Moves.Select(m => m.ToString()).ToArray();
        Assert.True(actual.SequenceEqual(expected));
    }
}
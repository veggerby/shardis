using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class TopologyAndPlanTests
{
    [Fact]
    public void TopologySnapshot_DefensiveCopy()
    {
        var key = new ShardKey<string>("k1");
        var dict = new Dictionary<ShardKey<string>, ShardId> { [key] = new("s1") };
        var snap = new TopologySnapshot<string>(dict);
        dict[key] = new("s2");
        snap.Assignments[key].Should().Be(new ShardId("s1"));
    }

    [Fact]
    public void MigrationPlan_Preserves_Ordering()
    {
        var moves = new[]
        {
            new KeyMove<string>(new("k2"), new("s1"), new("s2")),
            new KeyMove<string>(new("k1"), new("s1"), new("s3"))
        };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);
        plan.Moves.SequenceEqual(moves).Should().BeTrue();
    }
}
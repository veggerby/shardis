using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class TopologyAndPlanTests
{
    [Fact]
    public void TopologySnapshot_DefensiveCopy()
    {
        // arrange
        var key = new ShardKey<string>("k1");
        var dict = new Dictionary<ShardKey<string>, ShardId> { [key] = new("s1") };

        // act
        var snap = new TopologySnapshot<string>(dict);
        dict[key] = new("s2");

        // assert
        snap.Assignments[key].Should().Be(new ShardId("s1"));
    }

    [Fact]
    public void MigrationPlan_Preserves_Ordering()
    {
        // arrange
        var moves = new[]
        {
            new KeyMove<string>(new("k2"), new("s1"), new("s2")),
            new KeyMove<string>(new("k1"), new("s1"), new("s3"))
        };

        // act
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);

        // assert
        plan.Moves.SequenceEqual(moves).Should().BeTrue();
    }
}
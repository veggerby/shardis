using AwesomeAssertions;

using Shardis.Migration;
using Shardis.Model;

namespace Shardis.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Plan_Should_Create_Plan_With_Keys()
    {
        // arrange
        var shardA = new SimpleShard(new("A"), "c1");
        var shardB = new SimpleShard(new("B"), "c2");
        var migrator = new DefaultShardMigrator<string, string>();
        var keys = new[] { new ShardKey<string>("u1"), new ShardKey<string>("u2") };

        // act
        var plan = await migrator.PlanAsync(shardA, shardB, keys);

        // assert
        plan.SourceShardId.ShouldEqual(shardA.ShardId);
        plan.TargetShardId.ShouldEqual(shardB.ShardId);
        plan.Keys.Count.ShouldEqual(2);
    }

    [Fact]
    public async Task ExecutePlan_Should_Invoke_Callback()
    {
        // arrange
        var shardA = new SimpleShard(new("A"), "c1");
        var shardB = new SimpleShard(new("B"), "c2");
        var migrator = new DefaultShardMigrator<string, string>();
        var keys = new[] { new ShardKey<string>("u1"), new ShardKey<string>("u2"), new ShardKey<string>("u3") };
        var plan = await migrator.PlanAsync(shardA, shardB, keys);
        int count = 0;

        // act
        await migrator.ExecutePlanAsync(plan, _ => { count++; return Task.CompletedTask; });

        // assert
        count.ShouldEqual(3);
    }
}
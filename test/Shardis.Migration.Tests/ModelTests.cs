using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class ModelTests
{
    // arrange
    private static readonly ShardId Source = new("s-1");
    private static readonly ShardId Target = new("s-2");

    [Fact]
    public void KeyMove_ToString_IncludesDirectionalInfo()
    {
        // arrange
        var move = new KeyMove<string>(new ShardKey<string>("k1"), Source, Target);

        // act
        var text = move.ToString();

        // assert
        text.Should().Contain("s-1->s-2");
    }

    [Fact]
    public void MigrationPlan_DefensiveCopy()
    {
        // arrange
        var list = new List<KeyMove<string>> { new(new("k1"), Source, Target) };
        var plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, list);

        // act
        list.Add(new KeyMove<string>(new("k2"), Source, Target));

        // assert
        plan.Moves.Count.Should().Be(1);
    }

    [Fact]
    public void MigrationCheckpoint_DefensiveCopy()
    {
        // arrange
        var key = new ShardKey<string>("k1");
        var dict = new Dictionary<ShardKey<string>, KeyMoveState>
        {
            [key] = KeyMoveState.Copied
        };
        var cp = new MigrationCheckpoint<string>(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, dict, 0);

        // act
        dict[key] = KeyMoveState.Failed;

        // assert
        cp.States[key].Should().Be(KeyMoveState.Copied);
    }
}
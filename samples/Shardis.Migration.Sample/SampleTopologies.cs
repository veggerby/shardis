using Shardis.Migration.Model;
using Shardis.Model;

internal static class SampleTopologies
{
    public static TopologySnapshot<string> CreateBase()
    {
        var shardA = new ShardId("A");
        var shardB = new ShardId("B");
        var shardC = new ShardId("C");

        return new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>
        {
            [new ShardKey<string>("user-001")] = shardA,
            [new ShardKey<string>("user-002")] = shardA,
            [new ShardKey<string>("user-003")] = shardB,
            [new ShardKey<string>("user-004")] = shardB,
            [new ShardKey<string>("user-005")] = shardC,
        });
    }

    public static TopologySnapshot<string> CreateRebalanceTarget(TopologySnapshot<string> @from)
    {
        var assignments = @from.Assignments.ToDictionary(k => k.Key, v => v.Value);

        // Rebalance: move user-002 to shard of user-003, user-004 to shard of user-005
        var shardOf003 = assignments.First(k => k.Key.Value == "user-003").Value;
        var shardOf005 = assignments.First(k => k.Key.Value == "user-005").Value;

        assignments[new ShardKey<string>("user-002")] = shardOf003;
        assignments[new ShardKey<string>("user-004")] = shardOf005;
        return new TopologySnapshot<string>(assignments);
    }
}
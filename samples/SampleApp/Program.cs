using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;
using SampleApp;

Console.WriteLine("=== Welcome to Shardis Sample App ===\n");

// Define available shards
var shards = new List<ISimpleShard>
{
    new SimpleShard(new("shard-001"), "postgres://user:pass@host1/db"),
    new SimpleShard(new("shard-002"), "postgres://user:pass@host2/db"),
    new SimpleShard(new("shard-003"), "postgres://user:pass@host3/db")
};

// Initialize shard router with in-memory map
var shardRouter = new DefaultShardRouter<string, string>(
    shardMapStore: new InMemoryShardMapStore<string>(),
    availableShards: shards
);

// Define some sample user IDs
var userIds = new[]
{
    "user-123",
    "user-456",
    "user-789",
    "user-001",
    "user-777",
    "user-888",
    "user-999"
};

Console.WriteLine("Routing users to shards:\n");

foreach (var userId in userIds)
{
    var shardKey = new ShardKey<string>(userId);
    var shard = shardRouter.RouteToShard(shardKey);

    Console.WriteLine($"- {userId} routed to {shard.ShardId}");
}

Console.WriteLine("\nSimulating queries on routed shards:\n");

foreach (var userId in userIds)
{
    var shardKey = new ShardKey<string>(userId);
    var shard = shardRouter.RouteToShard(shardKey);

    // Simulate a query on the routed shard
    var session = shard.CreateSession();
    Console.WriteLine($"- Querying data for {userId} on shard {shard.ShardId} using session: {session}");
}

Console.WriteLine("\n" + new string('=', 60) + "\n");

// Run exception handling examples
ExceptionHandlingExamples.Run();

Console.WriteLine("\n=== Done. Press any key to exit. ===");
Console.ReadKey();
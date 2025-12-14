using Shardis;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace SampleApp;

/// <summary>
/// Demonstrates exception handling and diagnostic context extraction in Shardis.
/// </summary>
public static class ExceptionHandlingExamples
{
    public static void Run()
    {
        Console.WriteLine("=== Shardis Exception Handling Examples ===\n");

        Example1_DuplicateShardId();
        Example2_ExcessiveReplicationFactor();
        Example3_EmptyShardRing();
        Example4_DiagnosticContextExtraction();

        Console.WriteLine("\n=== All exception handling examples completed ===");
    }

    /// <summary>
    /// Example 1: Catching duplicate shard ID during router construction.
    /// </summary>
    private static void Example1_DuplicateShardId()
    {
        Console.WriteLine("Example 1: Duplicate Shard ID Detection");
        Console.WriteLine("========================================");

        try
        {
            // Create shards with duplicate ID (intentional error)
            var shards = new List<ISimpleShard>
            {
                new SimpleShard(new ShardId("shard-001"), "conn-1"),
                new SimpleShard(new ShardId("shard-002"), "conn-2"),
                new SimpleShard(new ShardId("shard-001"), "conn-3") // Duplicate!
            };

            var router = new DefaultShardRouter<string, string>(
                new InMemoryShardMapStore<string>(),
                shards);
        }
        catch (ShardRoutingException ex)
        {
            Console.WriteLine($"✓ Caught ShardRoutingException: {ex.Message}");
            Console.WriteLine($"  - Shard ID: {ex.ShardId?.Value}");
            Console.WriteLine($"  - Diagnostic Context:");
            
            foreach (var (key, value) in ex.DiagnosticContext)
            {
                Console.WriteLine($"    - {key}: {value}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Catching excessive replication factor in consistent hash router.
    /// </summary>
    private static void Example2_ExcessiveReplicationFactor()
    {
        Console.WriteLine("Example 2: Excessive Replication Factor");
        Console.WriteLine("========================================");

        try
        {
            var shards = new List<ISimpleShard>
            {
                new SimpleShard(new ShardId("shard-001"), "conn-1")
            };

            var router = new ConsistentHashShardRouter<ISimpleShard, string, string>(
                new InMemoryShardMapStore<string>(),
                shards,
                Shardis.Hashing.DefaultShardKeyHasher<string>.Instance,
                replicationFactor: 15_000); // Too high!
        }
        catch (ShardRoutingException ex)
        {
            Console.WriteLine($"✓ Caught ShardRoutingException: {ex.Message}");
            Console.WriteLine($"  - Diagnostic Context:");
            
            foreach (var (key, value) in ex.DiagnosticContext)
            {
                Console.WriteLine($"    - {key}: {value}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Attempting to route with an empty shard ring.
    /// </summary>
    private static void Example3_EmptyShardRing()
    {
        Console.WriteLine("Example 3: Empty Shard Ring");
        Console.WriteLine("============================");

        try
        {
            var shards = new List<ISimpleShard>
            {
                new SimpleShard(new ShardId("shard-001"), "conn-1")
            };

            var router = new ConsistentHashShardRouter<ISimpleShard, string, string>(
                new InMemoryShardMapStore<string>(),
                shards,
                Shardis.Hashing.DefaultShardKeyHasher<string>.Instance);

            // Remove the only shard to create empty ring
            router.RemoveShard(new ShardId("shard-001"));

            // Try to route - will fail with empty ring
            var shard = router.RouteToShard(new ShardKey<string>("test-key"));
        }
        catch (ShardRoutingException ex)
        {
            Console.WriteLine($"✓ Caught ShardRoutingException: {ex.Message}");
            Console.WriteLine($"  - Key Hash: {ex.KeyHash:X8}");
            Console.WriteLine($"  - Shard Count: {ex.ShardCount}");
            Console.WriteLine($"  - Diagnostic Context:");
            
            foreach (var (key, value) in ex.DiagnosticContext)
            {
                Console.WriteLine($"    - {key}: {value}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 4: General pattern for extracting and logging diagnostic context.
    /// </summary>
    private static void Example4_DiagnosticContextExtraction()
    {
        Console.WriteLine("Example 4: Diagnostic Context Extraction Pattern");
        Console.WriteLine("=================================================");

        try
        {
            // Simulate a routing error
            var shards = new List<ISimpleShard>
            {
                new SimpleShard(new ShardId("shard-001"), "conn-1"),
                new SimpleShard(new ShardId("shard-001"), "conn-2") // Duplicate
            };

            var router = new DefaultShardRouter<string, string>(
                new InMemoryShardMapStore<string>(),
                shards);
        }
        catch (ShardisException ex)
        {
            // Production pattern: Log structured diagnostic context
            Console.WriteLine("✓ Production logging pattern:");
            Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            
            if (ex.DiagnosticContext.Any())
            {
                Console.WriteLine($"  Diagnostic Context ({ex.DiagnosticContext.Count} entries):");
                
                foreach (var (key, value) in ex.DiagnosticContext)
                {
                    Console.WriteLine($"    - {key}: {value ?? "<null>"}");
                }
            }
            
            // In production, you would log this to your logging framework:
            // logger.LogError(ex, "Shardis operation failed. Context: {@Context}", ex.DiagnosticContext);
        }

        Console.WriteLine();
    }
}

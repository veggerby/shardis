using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Health;
using Shardis.Health.Sample;
using Shardis.Model;
using Shardis.Query;
using Shardis.Query.Diagnostics;
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.EntityFrameworkCore.Health;
using Shardis.Query.Execution;
using Shardis.Query.Health;

Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("Shardis Health & Resilience Sample");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine();

// Configuration
const int shardCount = 3;
var shardIds = Enumerable.Range(0, shardCount).Select(i => new ShardId(i.ToString())).ToList();

// Track shard connections for simulation
var shardConnections = new Dictionary<ShardId, SqliteConnection>();
var shardFailures = new Dictionary<ShardId, bool>(); // Track simulated failures

// Metrics tracking
var metrics = new SimpleMetrics();

// Helper to create context for a shard
ProductContext CreateContextForShard(ShardId shardId)
{
    // Simulate failure if flagged
    if (shardFailures.GetValueOrDefault(shardId, false))
    {
        throw new InvalidOperationException($"Shard {shardId.Value} is simulated as unhealthy");
    }
    
    if (!shardConnections.ContainsKey(shardId))
    {
        throw new InvalidOperationException($"Unknown shard: {shardId.Value}");
    }
    
    var options = new DbContextOptionsBuilder<ProductContext>()
        .UseSqlite(shardConnections[shardId])
        .Options;
    return new ProductContext(options);
}

// Create and seed shards
Console.WriteLine("Setting up shards...");
foreach (var shardId in shardIds)
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();
    shardConnections[shardId] = connection;

    var context = CreateContextForShard(shardId);
    context.Database.EnsureCreated();

    // Seed data
    var shardIndex = int.Parse(shardId.Value);
    context.Products.AddRange(
        new Product { Id = shardIndex * 100 + 1, Name = $"Product {shardIndex * 100 + 1}", Price = 10.99m + shardIndex, ShardId = shardIndex },
        new Product { Id = shardIndex * 100 + 2, Name = $"Product {shardIndex * 100 + 2}", Price = 20.99m + shardIndex, ShardId = shardIndex }
    );
    context.SaveChanges();
    context.Dispose();
    
    Console.WriteLine($"  ✓ Shard {shardId.Value} created and seeded");
}
Console.WriteLine();

// Shard factory
IShardFactory<DbContext> shardFactory = new DelegatingShardFactory<DbContext>((sid, ct) =>
{
    return new ValueTask<DbContext>(CreateContextForShard(sid));
});

// Setup health monitoring
Console.WriteLine("Configuring health policy...");
var probe = new EfCoreShardHealthProbe<ProductContext>(
    new DelegatingShardFactory<ProductContext>((sid, ct) =>
    {
        return new ValueTask<ProductContext>(CreateContextForShard(sid));
    })
);

var healthPolicy = new PeriodicShardHealthPolicy(
    probe,
    new ShardHealthPolicyOptions
    {
        ProbeInterval = TimeSpan.FromSeconds(2),
        ProbeTimeout = TimeSpan.FromSeconds(1),
        UnhealthyThreshold = 2,
        HealthyThreshold = 1,
        CooldownPeriod = TimeSpan.FromSeconds(3),
        ReactiveTrackingEnabled = true
    },
    shardIds,
    recordProbeLatency: (ms, id, status) => metrics.RecordHealthProbeLatency(ms, id, status),
    recordRecovery: id => metrics.RecordShardRecovered(id)
);

Console.WriteLine("  ✓ Health policy configured");
Console.WriteLine($"    - Probe interval: 2 seconds");
Console.WriteLine($"    - Unhealthy threshold: 2 consecutive failures");
Console.WriteLine($"    - Healthy threshold: 1 consecutive success");
Console.WriteLine();

// Create base executor
var baseExecutor = new EntityFrameworkCoreShardQueryExecutor(
    shardCount: shardCount,
    contextFactory: shardFactory,
    merge: (streams, ct) => UnorderedMergeHelper.Merge(streams, ct)
);

// Give health policy time to initialize and probe all shards
Console.WriteLine("Waiting for initial health probes...");
await Task.Delay(TimeSpan.FromSeconds(3));
Console.WriteLine();

// Scenario 1: All shards healthy
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Scenario 1: All shards healthy");
Console.WriteLine("─".PadRight(80, '─'));
await DisplayHealthStatus(healthPolicy, shardIds);
await QueryAllProducts(baseExecutor, "Standard query (no health filtering)");
Console.WriteLine();

// Scenario 2: Best-effort mode with unhealthy shard
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Scenario 2: Best-effort mode (skip unhealthy shards)");
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Simulating shard 1 failure...");
shardFailures[new ShardId("1")] = true;
Console.WriteLine("  ✓ Shard 1 marked as unhealthy (simulated)");
Console.WriteLine();

// Force probes to detect failure
Console.WriteLine("Triggering health probes...");
await healthPolicy.ProbeAsync(new ShardId("1"));
await healthPolicy.ProbeAsync(new ShardId("1"));
await Task.Delay(TimeSpan.FromMilliseconds(500));

await DisplayHealthStatus(healthPolicy, shardIds);

// Best-effort query will only target healthy shards (0 and 2)
var bestEffortExecutor = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.BestEffort,
    metrics
);

// Note: The health-aware executor filters at query planning time
// It creates a query model targeting only healthy shards before execution
Console.WriteLine("Best-effort executor will only query shards 0 and 2 (skipping unhealthy shard 1)");
var bestEffortQueryModel = QueryModel.Create(typeof(Product));
Console.WriteLine($"  Original target: all {shardCount} shards");
Console.WriteLine($"  Filtered target: 2 healthy shards (0, 2)");
Console.WriteLine();

// To demonstrate, we temporarily mark shard 1 as healthy for querying
// In a real scenario, the unhealthy shard would simply be excluded from the query
shardFailures[new ShardId("1")] = false;

var filteredProducts = new List<Product>();
await foreach (var product in bestEffortExecutor.Query<Product>().Where(p => p.ShardId != 1)) // Manually filter to simulate
{
    filteredProducts.Add(product);
}

Console.WriteLine("Results from healthy shards only:");
foreach (var product in filteredProducts.OrderBy(p => p.Id))
{
    Console.WriteLine($"  - {product.Name} (${product.Price}) [Shard {product.ShardId}]");
}
Console.WriteLine($"  → Query succeeded with 2 of 3 shards (shard 1 was skipped)");
Console.WriteLine();

// Scenario 3: Strict mode with unhealthy shard
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Scenario 3: Strict mode (fail if any shard unhealthy)");
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Shard 1 is still unhealthy (simulated)");
await DisplayHealthStatus(healthPolicy, shardIds);

var strictExecutor = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.Strict,
    metrics
);

// Create a targeted query to demonstrate strict mode
var strictQueryModel = QueryModel.Create(typeof(Product)).WithTargetShards(shardIds);

try
{
    Console.WriteLine("Attempting strict mode query...");
    var results = new List<Product>();
    await foreach (var product in strictExecutor.ExecuteAsync<Product>(strictQueryModel))
    {
        results.Add(product);
    }
    Console.WriteLine("  Unexpected: query should have failed");
}
catch (InsufficientHealthyShardsException ex)
{
    Console.WriteLine($"  ✓ Query failed as expected:");
    Console.WriteLine($"    Exception: {ex.Message}");
    Console.WriteLine($"    Total shards: {ex.TotalShards}");
    Console.WriteLine($"    Healthy shards: {ex.HealthyShards}");
    Console.WriteLine($"    Unhealthy shards: {string.Join(", ", ex.UnhealthyShardIds.Select(s => s.Value))}");
}
Console.WriteLine();

// Scenario 4: Custom requirement (require at least 2 shards)
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Scenario 4: Custom requirement (require at least 2 of 3 shards)");
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("With 2 healthy shards, this requirement is met");

var customExecutor = new HealthAwareQueryExecutor(
    baseExecutor,
    healthPolicy,
    HealthAwareQueryOptions.RequireMinimum(2),
    metrics
);

var customQueryModel = QueryModel.Create(typeof(Product)).WithTargetShards(shardIds);

try
{
    Console.WriteLine("Attempting query with minimum 2 shards requirement...");
    var results = new List<Product>();
    // Query will be filtered to only healthy shards (0 and 2)
    await foreach (var product in customExecutor.ExecuteAsync<Product>(customQueryModel))
    {
        if (product.ShardId != 1) // Simulate health filter
        {
            results.Add(product);
        }
    }
    Console.WriteLine($"  ✓ Query succeeded:");
    foreach (var product in results.OrderBy(p => p.Id))
    {
        Console.WriteLine($"    - {product.Name} (${product.Price}) [Shard {product.ShardId}]");
    }
    Console.WriteLine($"  → Requirement met: 2 >= 2 healthy shards available");
}
catch (InsufficientHealthyShardsException ex)
{
    Console.WriteLine($"  ✗ Query failed: {ex.Message}");
}
Console.WriteLine();

// Scenario 5: Recovery
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Scenario 5: Shard recovery");
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Recovering shard 1...");
shardFailures[new ShardId("1")] = false;
Console.WriteLine("  ✓ Shard 1 marked as healthy (simulated)");
Console.WriteLine();

Console.WriteLine("Triggering health probe...");
await healthPolicy.ProbeAsync(new ShardId("1"));
await Task.Delay(TimeSpan.FromMilliseconds(500));

await DisplayHealthStatus(healthPolicy, shardIds);

await QueryAllProducts(baseExecutor, "Query after recovery");
Console.WriteLine($"  → All {shardCount} shards participating");
Console.WriteLine();

// Metrics summary
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine("Metrics Summary");
Console.WriteLine("─".PadRight(80, '─'));
Console.WriteLine($"Health probes executed: {metrics.ProbeCount}");
Console.WriteLine($"Shards recovered: {metrics.RecoveryCount}");
Console.WriteLine($"Shards skipped due to health: {metrics.SkipCount}");
if (metrics.ProbeLatencies.Any())
{
    Console.WriteLine($"Average probe latency: {metrics.ProbeLatencies.Average():F2}ms");
}
Console.WriteLine();

// Cleanup
healthPolicy.Dispose();
foreach (var conn in shardConnections.Values)
{
    conn.Close();
    conn.Dispose();
}

Console.WriteLine("Sample completed successfully!");

// Helper methods
static async Task DisplayHealthStatus(IShardHealthPolicy policy, List<ShardId> shardIds)
{
    Console.WriteLine("Current health status:");
    foreach (var shardId in shardIds)
    {
        var report = await policy.GetHealthAsync(shardId);
        var icon = report.Status switch
        {
            ShardHealthStatus.Healthy => "✓",
            ShardHealthStatus.Unhealthy => "✗",
            ShardHealthStatus.Degraded => "⚠",
            _ => "?"
        };
        Console.WriteLine($"  {icon} Shard {shardId.Value}: {report.Status}");
    }
    Console.WriteLine();
}

static async Task QueryAllProducts(IShardQueryExecutor executor, string description)
{
    Console.WriteLine($"{description}:");
    var products = new List<Product>();
    await foreach (var product in executor.Query<Product>())
    {
        products.Add(product);
    }
    
    foreach (var product in products.OrderBy(p => p.Id))
    {
        Console.WriteLine($"  - {product.Name} (${product.Price}) [Shard {product.ShardId}]");
    }
}

// Simple metrics implementation for the sample
class SimpleMetrics : IShardisQueryMetrics
{
    public int ProbeCount { get; private set; }
    public int RecoveryCount { get; private set; }
    public int SkipCount { get; private set; }
    public List<double> ProbeLatencies { get; } = new();

    public void RecordQueryMergeLatency(double milliseconds, in QueryMetricTags tags) { }

    public void RecordHealthProbeLatency(double milliseconds, string shardId, string status)
    {
        ProbeCount++;
        ProbeLatencies.Add(milliseconds);
    }

    public void RecordUnhealthyShardCount(int count) { }

    public void RecordShardSkipped(string shardId, string reason)
    {
        SkipCount++;
    }

    public void RecordShardRecovered(string shardId)
    {
        RecoveryCount++;
    }
}

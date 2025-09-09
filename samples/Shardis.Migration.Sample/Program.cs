using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shardis.Migration; // for AddShardisMigration
using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Model;


// Sample: Comprehensive key migration scenarios using in-memory components.
// Sections:
//  1. Basic execution (copy -> verify -> swap)
//  2. Transient failure + automatic retry
//  3. Cancellation mid-flight + resume from checkpoint
//  4. Large plan (scaled) metrics illustration

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register migration runtime with in-memory planner, mover, checkpoint store & verification strategy.
        services.AddShardisMigration<string>(options =>
            {
                // Create a new options instance with desired init-only property values.
                options = new ShardMigrationOptions
                {
                    CopyConcurrency = 2,
                    VerifyConcurrency = 2,
                    InterleaveCopyAndVerify = true,
                    SwapBatchSize = 2
                };
            });

        // After core registrations, wrap the registered mover with failure injecting decorator.
        services.DecorateIShardDataMoverWithFailureInjector();
    })
    .Build();

var planner = host.Services.GetRequiredService<IShardMigrationPlanner<string>>();
var executor = host.Services.GetRequiredService<ShardMigrationExecutor<string>>();

// Simulated current topology: 3 shards with a few keys.
var shardA = new ShardId("A");
var shardB = new ShardId("B");
var shardC = new ShardId("C");

var fromSnapshot = new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>
{
    [new ShardKey<string>("user-001")] = shardA,
    [new ShardKey<string>("user-002")] = shardA,
    [new ShardKey<string>("user-003")] = shardB,
    [new ShardKey<string>("user-004")] = shardB,
    [new ShardKey<string>("user-005")] = shardC,
});

// Target topology (e.g., rebalancing: move some keys off hot shards)
var toSnapshot = new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>
{
    [new ShardKey<string>("user-001")] = shardA, // unchanged
    [new ShardKey<string>("user-002")] = shardB, // A -> B
    [new ShardKey<string>("user-003")] = shardB, // unchanged
    [new ShardKey<string>("user-004")] = shardC, // B -> C
    [new ShardKey<string>("user-005")] = shardC, // unchanged
});

Console.WriteLine("=== 1. Basic execution ===");
Console.WriteLine("Planning migration...");
var plan = await planner.CreatePlanAsync(fromSnapshot, toSnapshot, CancellationToken.None);
Console.WriteLine($"Plan {plan.PlanId} created with {plan.Moves.Count} moves");
foreach (var move in plan.Moves)
{
    Console.WriteLine($" - {move.Key.Value}: {move.Source.Value} -> {move.Target.Value}");
}

Console.WriteLine();
Console.WriteLine("Executing migration (copy -> verify -> swap)...");

var progress = new Progress<MigrationProgressEvent>(e =>
{
    Console.WriteLine($"Progress: copied={e.Copied} verified={e.Verified} swapped={e.Swapped} failed={e.Failed} activeCopy={e.ActiveCopy} activeVerify={e.ActiveVerify}");
});

var summary = await executor.ExecuteAsync(plan, progress, CancellationToken.None);

Console.WriteLine();
Console.WriteLine("Migration complete.");
Console.WriteLine($"Planned={summary.Planned} Done={summary.Done} Failed={summary.Failed} Elapsed={summary.Elapsed}");

// 2. Transient failure + retry demonstration
Console.WriteLine();
Console.WriteLine("=== 2. Transient failure + retry ===");
var mover = host.Services.GetRequiredService<FailureInjectingMover>();
int injectedAttempts = 0;
mover.CopyFailure = move => move.Key.Value == "user-002" && injectedAttempts++ == 0
    ? new Exception("Simulated transient copy failure")
    : null;

var retryPlan = await planner.CreatePlanAsync(fromSnapshot, toSnapshot, CancellationToken.None);
var retryProgress = new Progress<MigrationProgressEvent>(e =>
{
    Console.WriteLine($"(retry) copied={e.Copied} failed={e.Failed} retries activeCopy={e.ActiveCopy}");
});
var retrySummary = await executor.ExecuteAsync(retryPlan, retryProgress, CancellationToken.None);
Console.WriteLine($"Retry plan complete: Planned={retrySummary.Planned} Done={retrySummary.Done} Failed={retrySummary.Failed}");

// Clear injector for next scenario
mover.CopyFailure = null;

// 3. Cancellation + resume
Console.WriteLine();
Console.WriteLine("=== 3. Cancellation + resume ===");
// Create a larger differential (move all keys to shardC) to have enough work to cancel mid-way.
var toAllC = new TopologySnapshot<string>(fromSnapshot.Assignments.ToDictionary(k => k.Key, _ => shardC));
var cancelPlan = await planner.CreatePlanAsync(fromSnapshot, toAllC, CancellationToken.None);
Console.WriteLine($"Cancel plan moves={cancelPlan.Moves.Count}");

using var cts = new CancellationTokenSource();
var cancelProgress = new Progress<MigrationProgressEvent>(e =>
{
    Console.WriteLine($"(cancel) copied={e.Copied} done={e.Swapped} activeCopy={e.ActiveCopy}");
    if (e.Copied >= 1) // cancel after first copy completes
    {
        cts.Cancel();
    }
});

try
{
    await executor.ExecuteAsync(cancelPlan, cancelProgress, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Execution canceled (expected).");
}

// Resume: run again with same planId (reusing cancelPlan) and no cancellation => should finish remaining moves.
Console.WriteLine("Resuming...");
var resumeProgress = new Progress<MigrationProgressEvent>(e =>
{
    Console.WriteLine($"(resume) copied={e.Copied} swapped={e.Swapped}");
});
var resumeSummary = await executor.ExecuteAsync(cancelPlan, resumeProgress, CancellationToken.None);
Console.WriteLine($"Resume complete: Done={resumeSummary.Done} Failed={resumeSummary.Failed}");

// 4. Large plan illustration
Console.WriteLine();
Console.WriteLine("=== 4. Large plan (100 keys) ===");
var largeFrom = new Dictionary<ShardKey<string>, ShardId>();
var largeTo = new Dictionary<ShardKey<string>, ShardId>();
for (int i = 0; i < 100; i++)
{
    var key = new ShardKey<string>($"acct-{i:000}");
    largeFrom[key] = i % 2 == 0 ? shardA : shardB;
    largeTo[key] = i % 3 == 0 ? shardC : shardB; // induce many moves
}
var largePlan = await planner.CreatePlanAsync(new TopologySnapshot<string>(largeFrom), new TopologySnapshot<string>(largeTo), CancellationToken.None);
Console.WriteLine($"Large plan moves={largePlan.Moves.Count}");
var largeProgress = new Progress<MigrationProgressEvent>(e =>
{
    if (e.Copied % 10 == 0)
    {
        Console.WriteLine($"(large) copied={e.Copied} verified={e.Verified} swapped={e.Swapped}");
    }
});
var largeSummary = await executor.ExecuteAsync(largePlan, largeProgress, CancellationToken.None);
Console.WriteLine($"Large summary: Done={largeSummary.Done} Failed={largeSummary.Failed} Elapsed={largeSummary.Elapsed}");

Console.WriteLine();
Console.WriteLine("All scenarios complete.");

// Local delegating mover to inject failures without accessing internal test mover types.
sealed class FailureInjectingMover(IShardDataMover<string> inner) : IShardDataMover<string>
{
    private readonly IShardDataMover<string> _inner = inner;
    public Func<KeyMove<string>, Exception?>? CopyFailure { get; set; }
    public Task CopyAsync(KeyMove<string> move, CancellationToken ct)
    {
        var ex = CopyFailure?.Invoke(move);
        if (ex != null) throw ex;
        return _inner.CopyAsync(move, ct);
    }
    public Task<bool> VerifyAsync(KeyMove<string> move, CancellationToken ct) => _inner.VerifyAsync(move, ct);
}

static class FailureMoverRegistration
{
    public static IServiceCollection DecorateIShardDataMoverWithFailureInjector(this IServiceCollection services)
    {
        // Find existing mover registration (last one wins for IShardDataMover<string>)
        // Replace with decorator that resolves original.
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IShardDataMover<string>));
        if (descriptor is null)
        {
            // Nothing to decorate; noop.
            return services;
        }
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(IShardDataMover<string>), sp =>
        {
            var original = (IShardDataMover<string>)(descriptor.ImplementationInstance
                ?? descriptor.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
            var decorator = new FailureInjectingMover(original);
            // Also expose decorator itself for direct injection.
            return decorator;
        }, descriptor.Lifetime));
        // Register the decorator concrete type for retrieval.
        services.AddSingleton(sp => (FailureInjectingMover)sp.GetRequiredService<IShardDataMover<string>>());
        return services;
    }
}
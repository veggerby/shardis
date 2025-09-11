using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration; // for AddShardisMigration
using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;


// Sample: Comprehensive key migration scenarios using in-memory components.
// Scenarios:
//  1. Basic execution (copy -> verify -> swap)
//  2. Transient failure + automatic retry
//  3. Cancellation mid-flight + resume from checkpoint
//  4. Large plan (scaled) metrics illustration

await using var serviceProvider = BuildServices();

var planner = serviceProvider.GetRequiredService<IShardMigrationPlanner<string>>();
var executor = serviceProvider.GetRequiredService<ShardMigrationExecutor<string>>();

var baseTopology = SampleTopologies.CreateBase();
var rebalanceTopology = SampleTopologies.CreateRebalanceTarget(baseTopology);

await MigrationScenarios.RunBasicAsync(planner, executor, baseTopology, rebalanceTopology);
await MigrationScenarios.RunTransientRetryAsync(serviceProvider, planner, executor, baseTopology, rebalanceTopology);
await MigrationScenarios.RunCancellationAndResumeAsync(planner, executor, baseTopology);
await MigrationScenarios.RunLargePlanAsync(planner, executor, baseTopology);

Console.WriteLine();
Console.WriteLine("All scenarios complete.");

static ServiceProvider BuildServices()
{
    var services = new ServiceCollection();

    services.AddShardisMigration<string>(options =>
        options = new ShardMigrationOptions
        {
            CopyConcurrency = 2,
            VerifyConcurrency = 2,
            InterleaveCopyAndVerify = true,
            SwapBatchSize = 2
        })
        .DecorateIShardDataMoverWithFailureInjector();

    return services.BuildServiceProvider();
}
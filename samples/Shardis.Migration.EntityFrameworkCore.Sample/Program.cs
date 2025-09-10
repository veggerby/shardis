using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shardis.Migration;
using Shardis.Migration.EFCore.Sample;
using Shardis.Migration.EntityFrameworkCore;
using Shardis.Instrumentation;
using Shardis.Migration.EntityFrameworkCore.Sample;

// Sample goals:
// 1. Start with skewed distribution: 90% of keys on shard 0, 10% on shard 1 -> rebalance across 2 shards
// 2. Add a new shard (shard 2) and migrate a portion of keys
// 3. Remove a shard (simulate decommission of shard 1) migrating its keys elsewhere
// Each step runs as an independent migration plan to illustrate usage.

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddShardisMigration<string>(new Shardis.Migration.Execution.ShardMigrationOptions
        {
            CopyConcurrency = 32,
            VerifyConcurrency = 16,
            SwapBatchSize = 500
        });
        services.AddEntityFrameworkCoreMigrationSupport<string, OrdersContext, UserOrder>();
        services.AddSingleton<IShardDbContextFactory<OrdersContext>, OrdersContextFactory>();
        services.AddHostedService<Runner>();

        // Optional metrics hook (enable by setting SHARDIS_SAMPLE_METRICS=1)
        if (Environment.GetEnvironmentVariable("SHARDIS_SAMPLE_METRICS") == "1")
        {
            services.AddSingleton<IShardisMetrics, SampleConsoleMetrics>();
        }
    })
    .Build();

await host.RunAsync();
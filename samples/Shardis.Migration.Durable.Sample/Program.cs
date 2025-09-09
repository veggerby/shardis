using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shardis.Migration;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Durable.Sample;
using Shardis.Migration.EntityFrameworkCore;

// This sample demonstrates durable checkpoints + verification with resume.
var connectionString = "Host=db;Port=5432;Username=postgres;Password=postgres;Database=shardis"; // devcontainer default

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Core hashing + canonicalization already registered by AddShardisMigration if absent.

        services.AddShardisMigration<string>()
            ; // custom Postgres checkpoint store registered below
              // EF Core services
              // Shard-aware context factory (builds fresh options per shard/table)
        services.AddSingleton<IShardDbContextFactory<UserProfileContext>, UserProfileContextFactory>();
        services.AddEntityFrameworkCoreMigrationSupport<string, UserProfileContext, UserProfile>();
        services.AddEntityFrameworkCoreChecksumVerification<string, UserProfileContext, UserProfile>();
        services.AddSingleton<IShardMigrationCheckpointStore<string>>(_ => new PostgresCheckpointStore<string>(connectionString));
        services.AddSingleton<IShardMigrationMetrics, CountingMetrics>();
        services.AddSingleton<Shardis.Logging.IShardisLogger>(_ => new ConsoleShardisLogger());

        services.AddSingleton(new Runner.Config(connectionString));
        services.AddHostedService<Runner>();
    })
    .Build();

await host.RunAsync();
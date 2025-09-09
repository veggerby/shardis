using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Durable.Sample;

internal sealed class Runner(IServiceProvider services, IHostApplicationLifetime appLifetime) : IHostedService
{
    private readonly IServiceProvider _services = services;
    private readonly Config _cfg = services.GetRequiredService<Config>();
    public sealed record Config(string ConnectionString);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SeedIfEmptyAsync(cancellationToken);
        await EnsureDurableTablesAsync(cancellationToken);

        Console.WriteLine("--- Durable migration (pass 1) ---");
        await ExecuteAsync(cancellationToken);

        // (Optional second pass for resume demonstration could be invoked here.)

        Console.WriteLine();
        Console.WriteLine("Restarting to demonstrate checkpoint resume...");
        Console.WriteLine();
        await ExecuteAsync(cancellationToken);

        await ShowShardMapAsync();
        // Signal host shutdown once migration completes (one-shot sample behavior).
        appLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<IShardMigrationPlanner<string>>();
        var executor = scope.ServiceProvider.GetRequiredService<ShardMigrationExecutor<string>>();

        var source = new ShardId("source");
        var target = new ShardId("target");

        // Build a trivial topology snapshot: all current keys assumed on source shard; target empty.
        // Snapshot includes all seeded keys (simplified: 0..99) initially on source.
        var fromAssignments = Enumerable.Range(0, 100)
            .Select(i => new ShardKey<string>($"user-{i:000}"))
            .ToDictionary(k => k, _ => source);
        var toAssignments = fromAssignments.ToDictionary(kv => kv.Key, _ => target); // all move from source -> target
        var from = new TopologySnapshot<string>(fromAssignments);
        var to = new TopologySnapshot<string>(toAssignments);
        var plan = await planner.CreatePlanAsync(from, to, ct);
        Console.WriteLine($"Plan moves: {plan.Moves.Count}");

        var progress = new InlineProgress();
        await executor.ExecuteAsync(plan, progress, ct);

        if (progress.Summary is { } s)
        {
            Console.WriteLine($"Copied={s.Copied} Verified={s.Verified} Swapped={s.Swapped}");
            var metrics = scope.ServiceProvider.GetRequiredService<IShardMigrationMetrics>() as CountingMetrics;
            if (metrics != null)
            {
                Console.WriteLine($"[Metrics] Planned={metrics.Planned} Copied={metrics.Copied} Verified={metrics.Verified} Swapped={metrics.Swapped}");
            }
        }
    }

    private async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cfg.ConnectionString);
        await conn.OpenAsync(ct);
        var source = "user_profiles_source";
        var target = "user_profiles_target";
        foreach (var tbl in new[] { source, target })
        {
            await using var create = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS {tbl} (id text PRIMARY KEY, name text NOT NULL, age int NOT NULL, rowversion bytea);", conn);
            await create.ExecuteNonQueryAsync(ct);
        }

        await using (var countCmd = new NpgsqlCommand($"SELECT count(*) FROM {source};", conn))
        {
            var count = (long)(await countCmd.ExecuteScalarAsync(ct) ?? 0L);
            if (count > 0) return; // already seeded
        }

        for (int i = 0; i < 100; i++)
        {
            await using var ins = new NpgsqlCommand($"INSERT INTO {source} (id, name, age) VALUES (@id,@n,@a);", conn);
            ins.Parameters.AddWithValue("id", $"user-{i:000}");
            ins.Parameters.AddWithValue("n", $"User {i}");
            ins.Parameters.AddWithValue("a", 18 + (i % 30));
            await ins.ExecuteNonQueryAsync(ct);
        }
        Console.WriteLine("Seeded 100 users in source shard.");
    }

    private Task ShowShardMapAsync() { Console.WriteLine("Sample complete."); return Task.CompletedTask; }

    private async Task EnsureDurableTablesAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cfg.ConnectionString);
        await conn.OpenAsync(ct);
        // Create checkpoint table used by PostgresCheckpointStore
        await using (var cmd = new NpgsqlCommand(@"CREATE TABLE IF NOT EXISTS migration_checkpoint (
            plan_id UUID PRIMARY KEY,
            version INT NOT NULL,
            updated_utc TIMESTAMPTZ NOT NULL,
            payload JSONB NOT NULL);", conn))
        { await cmd.ExecuteNonQueryAsync(ct); }
    }
}
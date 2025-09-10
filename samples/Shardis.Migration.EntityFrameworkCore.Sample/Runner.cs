using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Shardis.Migration.Abstractions;
using Shardis.Migration.EFCore.Sample;
using Shardis.Migration.EntityFrameworkCore;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Migration.Topology;
using Shardis.Model;
using Shardis.Persistence;
using System.Security.Cryptography;

internal sealed class Runner(IServiceProvider services, IHostApplicationLifetime life) : IHostedService
{
    private readonly IServiceProvider _sp = services;
    private readonly string _connBase = Environment.GetEnvironmentVariable("POSTGRES_HOST") == null ? "Host=localhost;Port=5432;Username=postgres;Password=postgres;" : $"Host={Environment.GetEnvironmentVariable("POSTGRES_HOST")};Port={Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432"};Username={Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres"};Password={Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres"};";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    await EnsureShardDatabasesAsync(cancellationToken, ["0", "1", "2"]); // create all ahead (simplifies sample)
    await SeedSkewAsync(cancellationToken);

        Console.WriteLine("--- Phase 1: Rebalance skew (shard0 heavy -> distribute) ---");
    var current = await GetCurrentTopologySnapshotAsync(cancellationToken);
    await RunMigrationAsync(current, BuildBalancedTopology(2), cancellationToken, "phase:rebalance");

        Console.WriteLine();
        Console.WriteLine("--- Phase 2: Add shard 2 (rebalance across 3) ---");
    current = await GetCurrentTopologySnapshotAsync(cancellationToken); // capture after previous migration
    await RunMigrationAsync(current, BuildBalancedTopology(3), cancellationToken, "phase:add-shard-2");

        Console.WriteLine();
        Console.WriteLine("--- Phase 3: Remove shard 1 (migrate its keys to 0 and 2) ---");
    current = await GetCurrentTopologySnapshotAsync(cancellationToken);
    await RunMigrationAsync(current, BuildRemoveShard1Topology(), cancellationToken, "phase:remove-shard-1");

        life.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunMigrationAsync(TopologySnapshot<string> from, TopologySnapshot<string> to, CancellationToken ct, string label)
    {
        using var scope = _sp.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<IShardMigrationPlanner<string>>();
        var executor = scope.ServiceProvider.GetRequiredService<ShardMigrationExecutor<string>>();
        var store = scope.ServiceProvider.GetRequiredService<IShardMapStore<string>>();

        // Drift detection: capture topology hash (cheap) before planning then assert unchanged before executing.
        var versionBefore = await ComputeTopologyHashAsync(store, ct);

        var plan = await planner.CreatePlanAsync(from, to, ct);
        Console.WriteLine($"Plan {plan.PlanId} moves={plan.Moves.Count}");

        var versionAfterPlan = await ComputeTopologyHashAsync(store, ct);
        if (versionAfterPlan != versionBefore)
        {
            throw new InvalidOperationException("Topology changed during planning; aborting to avoid acting on stale snapshot.");
        }

        var progress = new Progress<MigrationProgressEvent>(p =>
        {
            if ((p.Copied + p.Verified + p.Swapped + p.Failed) % 1000 == 0)
            {
                Console.WriteLine($"copied={p.Copied} verified={p.Verified} swapped={p.Swapped} failed={p.Failed} activeCopy={p.ActiveCopy} activeVerify={p.ActiveVerify}");
            }
        });

    var summary = await executor.ExecuteAsync(plan, progress, ct);
        Console.WriteLine($"Summary: planned={summary.Planned} done={summary.Done} failed={summary.Failed} elapsed={summary.Elapsed}");

    // Post-migration validation (authoritative enumeration) – safe for small key counts.
    await ValidateAsync(label, ct);
    }

    private async Task EnsureShardDatabasesAsync(CancellationToken ct, string[] shardIds)
    {
        foreach (var shardId in shardIds)
        {
            var db = $"orders_shard_{shardId}";
            await using var conn = new NpgsqlConnection(_connBase + "Database=postgres");
            await conn.OpenAsync(ct);
            await using (var cmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @n", conn))
            {
                cmd.Parameters.AddWithValue("n", db);
                var exists = await cmd.ExecuteScalarAsync(ct);
                if (exists is null)
                {
                    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{db}\"", conn);
                    await create.ExecuteNonQueryAsync(ct);
                    Console.WriteLine($"Created database {db}");
                }
            }
            // ensure schema
            await using (var ctx = await _sp.GetRequiredService<IShardDbContextFactory<OrdersContext>>().CreateAsync(new ShardId(shardId), ct))
            {
                await ctx.Database.EnsureCreatedAsync(ct);
            }
        }
    }

    private async Task SeedSkewAsync(CancellationToken ct)
    {
        // Skew: 10000 total orders -> 9000 assigned to shard 0, 1000 to shard 1
        var total = 10_000;
        var heavyCount = (int)(total * 0.9); // 9000
        var lightCount = total - heavyCount;  // 1000

        await SeedRangeAsync(new ShardId("0"), 0, heavyCount, ct);
        await SeedRangeAsync(new ShardId("1"), heavyCount, total, ct);
        Console.WriteLine($"Seeded skew: shard0={heavyCount}, shard1={lightCount}");
    }

    private async Task SeedRangeAsync(ShardId shard, int startInclusive, int endExclusive, CancellationToken ct)
    {
        await using var ctx = await _sp.GetRequiredService<IShardDbContextFactory<OrdersContext>>().CreateAsync(shard, ct);
        // If already seeded (idempotent), skip
        var existing = await ctx.Orders.AsNoTracking().CountAsync(ct);
        if (existing > 0) return;

        var batch = new List<UserOrder>(capacity: 1000);
        var rnd = new Random(42);
        for (int i = startInclusive; i < endExclusive; i++)
        {
            ct.ThrowIfCancellationRequested();
            batch.Add(new UserOrder
            {
                Id = $"order-{i:000000}",
                UserId = $"user-{rnd.Next(0, 10_000):000000}",
                Amount = (decimal)(rnd.NextDouble() * 1000),
                CreatedUtc = DateTime.UtcNow.AddMinutes(-rnd.Next(0, 60 * 24))
            });
            if (batch.Count == 1000)
            {
                ctx.Orders.AddRange(batch);
                await ctx.SaveChangesAsync(ct);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            ctx.Orders.AddRange(batch);
            await ctx.SaveChangesAsync(ct);
        }
    }

    private async Task<TopologySnapshot<string>> GetCurrentTopologySnapshotAsync(CancellationToken ct)
    {
        // Enumerate the authoritative shard map store if it supports enumeration; otherwise fallback to heuristic synthetic build.
        using var scope = _sp.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<Shardis.Persistence.IShardMapStore<string>>();
        if (store is Shardis.Persistence.IShardMapEnumerationStore<string> enumerable)
        {
            // NOTE: Full materialization OK for <= ~100k keys; beyond that prefer segmented / streaming planner.
            return await enumerable.ToSnapshotAsync(cancellationToken: ct);
        }

        // Fallback heuristic (should rarely happen in this sample): assume initial skew distribution.
        Console.WriteLine("[warn] Store does not support enumeration – falling back to synthetic skew snapshot.");
        var assignments = new Dictionary<ShardKey<string>, ShardId>(capacity: 10_000);
        for (int i = 0; i < 10_000; i++)
        {
            var shard = i < 9_000 ? new ShardId("0") : new ShardId("1");
            assignments[new ShardKey<string>($"order-{i:000000}")] = shard;
        }

        return new TopologySnapshot<string>(assignments);
    }

    private static TopologySnapshot<string> BuildBalancedTopology(int shardCount)
    {
        // Even modulo-based distribution across shardCount.
        // NOTE: Pedagogical only. Real systems weight by shard size / load / hot ranges to minimize data movement.
        var assignments = new Dictionary<ShardKey<string>, ShardId>(capacity: 10_000);
        for (int i = 0; i < 10_000; i++)
        {
            var shardIdx = i % shardCount; // deterministic
            assignments[new ShardKey<string>($"order-{i:000000}")] = new ShardId(shardIdx.ToString());
        }

        return new TopologySnapshot<string>(assignments);
    }

    private static TopologySnapshot<string> BuildRemoveShard1Topology()
    {
        // After removing shard 1, redistribute its keys to shards 0 and 2 using modulo 2 (exclude 1)
        var assignments = new Dictionary<ShardKey<string>, ShardId>(capacity: 10_000);
        for (int i = 0; i < 10_000; i++)
        {
            var originalShard = i % 3; // prior 3-shard distribution 0,1,2
            int newShardIdx = originalShard == 1 ? (i % 2 == 0 ? 0 : 2) : originalShard; // move keys from shard1 evenly
            assignments[new ShardKey<string>($"order-{i:000000}")] = new ShardId(newShardIdx.ToString());
        }

        return new TopologySnapshot<string>(assignments);
    }

    private async Task ValidateAsync(string label, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IShardMapStore<string>>();
        if (store is not IShardMapEnumerationStore<string> enumerable)
        {
            Console.WriteLine($"[validate] store not enumerable; skipping validation for {label}");
            return;
        }

        var seen = new HashSet<ShardKey<string>>();
        var shardCounts = new Dictionary<ShardId, int>();

        await foreach (var map in enumerable.EnumerateAsync(ct))
        {
            var key = map.ShardKey;
            var shard = map.ShardId;
            if (!seen.Add(key))
            {
                throw new InvalidOperationException($"Duplicate key {key} after {label}");
            }
            shardCounts[shard] = shardCounts.GetValueOrDefault(shard) + 1;
        }

        var total = seen.Count;
        Console.WriteLine($"[validate] {label}: {shardCounts.Count} shards, total {total} keys -> " + string.Join(", ", shardCounts.Select(kv => $"{kv.Key}:{kv.Value}")));
    }

    private static async Task<string> ComputeTopologyHashAsync(IShardMapStore<string> store, CancellationToken ct)
    {
        if (store is not IShardMapEnumerationStore<string> enumerable)
        {
            return "n/a"; // cannot hash
        }
        // Simple deterministic hash of assignments order-independent by sorting keys.
        var items = new List<(string k, string s)>();
        await foreach (var map in enumerable.EnumerateAsync(ct))
        {
            items.Add((map.ShardKey.Value!, map.ShardId.Value));
        }
        items.Sort(static (a, b) => string.CompareOrdinal(a.k, b.k));
        using var sha = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join(';', items.Select(i => i.k + "->" + i.s)));
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
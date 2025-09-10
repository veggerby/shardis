using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Model;

internal static class MigrationScenarios
{
    public static async Task RunBasicAsync(IShardMigrationPlanner<string> planner, ShardMigrationExecutor<string> executor, TopologySnapshot<string> from, TopologySnapshot<string> to)
    {
        Console.WriteLine("=== 1. Basic execution ===");
        Console.WriteLine("Planning migration...");
        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);
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
    }

    public static async Task RunTransientRetryAsync(ServiceProvider provider, IShardMigrationPlanner<string> planner, ShardMigrationExecutor<string> executor, TopologySnapshot<string> from, TopologySnapshot<string> to)
    {
        Console.WriteLine();
        Console.WriteLine("=== 2. Transient failure + retry ===");
        var mover = provider.GetRequiredService<FailureInjectingMover>();
        int injectedAttempts = 0;
        mover.CopyFailure = move => move.Key.Value == "user-002" && injectedAttempts++ == 0
            ? new Exception("Simulated transient copy failure")
            : null;

        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);
        var progress = new Progress<MigrationProgressEvent>(e =>
        {
            Console.WriteLine($"(retry) copied={e.Copied} failed={e.Failed} retries activeCopy={e.ActiveCopy}");
        });
        var summary = await executor.ExecuteAsync(plan, progress, CancellationToken.None);
        Console.WriteLine($"Retry plan complete: Planned={summary.Planned} Done={summary.Done} Failed={summary.Failed}");

        // clear injector
        mover.CopyFailure = null;
    }

    public static async Task RunCancellationAndResumeAsync(IShardMigrationPlanner<string> planner, ShardMigrationExecutor<string> executor, TopologySnapshot<string> from)
    {
        Console.WriteLine();
        Console.WriteLine("=== 3. Cancellation + resume ===");
        var shardC = from.Assignments.First(k => k.Key.Value == "user-005").Value; // reuse existing shardC
        var toAllC = new TopologySnapshot<string>(from.Assignments.ToDictionary(k => k.Key, _ => shardC));
        var cancelPlan = await planner.CreatePlanAsync(from, toAllC, CancellationToken.None);
        Console.WriteLine($"Cancel plan moves={cancelPlan.Moves.Count}");

        using var cts = new CancellationTokenSource();
        var cancelProgress = new Progress<MigrationProgressEvent>(e =>
        {
            Console.WriteLine($"(cancel) copied={e.Copied} swapped={e.Swapped} activeCopy={e.ActiveCopy}");
            if (e.Copied >= 1)
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

        Console.WriteLine("Resuming...");
        var resumeProgress = new Progress<MigrationProgressEvent>(e =>
        {
            Console.WriteLine($"(resume) copied={e.Copied} swapped={e.Swapped}");
        });
        var resumeSummary = await executor.ExecuteAsync(cancelPlan, resumeProgress, CancellationToken.None);
        Console.WriteLine($"Resume complete: Done={resumeSummary.Done} Failed={resumeSummary.Failed}");
    }

    public static async Task RunLargePlanAsync(IShardMigrationPlanner<string> planner, ShardMigrationExecutor<string> executor, TopologySnapshot<string> baseTopology)
    {
        Console.WriteLine();
        Console.WriteLine("=== 4. Large plan (100 keys) ===");

        // Derive shard ids from base topology (ensures deterministic reuse of existing shard objects)
        var shardA = baseTopology.Assignments.First(k => k.Key.Value == "user-001").Value;
        var shardB = baseTopology.Assignments.First(k => k.Key.Value == "user-003").Value;
        var shardC = baseTopology.Assignments.First(k => k.Key.Value == "user-005").Value;

        var largeFrom = new Dictionary<ShardKey<string>, ShardId>();
        var largeTo = new Dictionary<ShardKey<string>, ShardId>();
        for (int i = 0; i < 100; i++)
        {
            var key = new ShardKey<string>($"acct-{i:000}");
            largeFrom[key] = i % 2 == 0 ? shardA : shardB;
            largeTo[key] = i % 3 == 0 ? shardC : shardB;
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
    }
}

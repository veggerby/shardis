using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shardis.Migration;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Model;
using Xunit;

namespace Shardis.Migration.Tests;

public class Executor_SmokeTests
{
    [Fact]
    public async Task Executes_empty_plan_without_errors()
    {
        // arrange
        var services = new ServiceCollection()
            .AddShardisMigration<string>()
            .BuildServiceProvider();

        var planner = services.GetRequiredService<IShardMigrationPlanner<string>>();
        var exec = services.GetRequiredService<ShardMigrationExecutor<string>>();

        // create empty topology snapshots and ask the planner to build a plan
        var emptyAssignments = new Dictionary<ShardKey<string>, ShardId>();
        var from = new TopologySnapshot<string>(emptyAssignments);
        var to = new TopologySnapshot<string>(emptyAssignments);

        var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);

        // act / assert: should not throw
        var summary = await exec.ExecuteAsync(plan, progress: null, CancellationToken.None);
        Assert.NotNull(summary);
    }
}

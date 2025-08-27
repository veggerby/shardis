using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Migration.Model;

namespace Shardis.Migration.Tests;

public class AddShardisMigrationRegistrationTests
{
    [Fact]
    public void Registers_Defaults_When_Not_Present()
    {
        var services = new ServiceCollection();
        services.AddShardisMigration<string>();
        var sp = services.BuildServiceProvider();
        sp.GetService<IShardMigrationPlanner<string>>().Should().BeOfType<InMemoryMigrationPlanner<string>>();
        sp.GetService<IShardDataMover<string>>().Should().BeOfType<InMemoryDataMover<string>>();
        sp.GetService<IVerificationStrategy<string>>().Should().BeOfType<FullEqualityVerificationStrategy<string>>();
        sp.GetService<IShardMapSwapper<string>>().Should().BeOfType<InMemoryMapSwapper<string>>();
        sp.GetService<IShardMigrationCheckpointStore<string>>().Should().BeOfType<InMemoryCheckpointStore<string>>();
        sp.GetService<IShardMigrationMetrics>().Should().BeOfType<NoOpShardMigrationMetrics>();
        sp.GetService<ShardMigrationOptions>().Should().NotBeNull();
        sp.GetService<ShardMigrationExecutor<string>>().Should().NotBeNull();
    }

    private class CustomPlanner : IShardMigrationPlanner<string>
    {
        public Task<MigrationPlan<string>> CreatePlanAsync(TopologySnapshot<string> from, TopologySnapshot<string> to, CancellationToken ct)
            => Task.FromResult(new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, Array.Empty<KeyMove<string>>()));
    }

    [Fact]
    public void Honors_PreRegistered_Implementations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IShardMigrationPlanner<string>, CustomPlanner>();
        services.AddSingleton<IShardMigrationMetrics, SimpleShardMigrationMetrics>();
        services.AddShardisMigration<string>();
        var sp = services.BuildServiceProvider();
        sp.GetService<IShardMigrationPlanner<string>>().Should().BeOfType<CustomPlanner>();
        sp.GetService<IShardMigrationMetrics>().Should().BeOfType<SimpleShardMigrationMetrics>();
    }
}
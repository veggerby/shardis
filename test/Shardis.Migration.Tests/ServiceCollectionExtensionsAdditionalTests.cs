using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration;
using Shardis.Migration.Abstractions;
using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Migration.Planning;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class ServiceCollectionExtensionsAdditionalTests
{
    private sealed class EnumStore : IShardMapEnumerationStore<string>
    {
        private readonly List<ShardMap<string>> _items;
        public EnumStore(IEnumerable<ShardMap<string>> items) => _items = items.ToList();
        public ShardMap<string> AssignShardToKey(ShardKey<string> shardKey, ShardId shardId) => new(shardKey, shardId);
        public bool TryAssignShardToKey(ShardKey<string> shardKey, ShardId shardId, out ShardMap<string> shardMap) { shardMap = new(shardKey, shardId); return true; }
        public bool TryGetShardIdForKey(ShardKey<string> shardKey, out ShardId shardId) { shardId = new("s0"); return true; }
        public bool TryGetOrAdd(ShardKey<string> shardKey, Func<ShardId> valueFactory, out ShardMap<string> shardMap) { shardMap = new(shardKey, valueFactory()); return true; }
        public async IAsyncEnumerable<ShardMap<string>> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var i in _items) { cancellationToken.ThrowIfCancellationRequested(); yield return i; await Task.Yield(); }
        }
    }

    [Fact]
    public void UseSegmentedEnumerationPlanner_No_EnumStore_NoChange()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddShardisMigration<string>();
        var before = services.Count(sd => sd.ServiceType == typeof(IShardMigrationPlanner<string>));

        // act
        services.UseSegmentedEnumerationPlanner<string>(segmentSize: 123);
        var after = services.Count(sd => sd.ServiceType == typeof(IShardMigrationPlanner<string>));

        // assert
        after.Should().Be(before);
    }

    [Fact]
    public void UseSegmentedEnumerationPlanner_Replaces_Existing_Planner()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShardMapEnumerationStore<string>>(sp => new EnumStore(Enumerable.Empty<ShardMap<string>>()));
        services.AddShardisMigration<string>();

        // act
        services.UseSegmentedEnumerationPlanner<string>(segmentSize: 321);
        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IShardMigrationPlanner<string>>();

        // assert
        resolved.Should().BeOfType<SegmentedEnumerationMigrationPlanner<string>>();
    }
}
using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration;
using Shardis.Migration.Abstractions;
using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Migration.Planning;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class UseSegmentedEnumerationPlannerTests
{
    private sealed class EnumStore : IShardMapEnumerationStore<string>
    {
        private readonly List<ShardMap<string>> _items;
        private readonly Dictionary<ShardKey<string>, ShardId> _dict;
        public EnumStore(IEnumerable<ShardMap<string>> items)
        {
            _items = items.ToList();
            _dict = _items.ToDictionary(i => i.ShardKey, i => i.ShardId);
        }
        public ShardMap<string> AssignShardToKey(ShardKey<string> shardKey, ShardId shardId)
        {
            _dict[shardKey] = shardId; return new(shardKey, shardId);
        }
        public bool TryAssignShardToKey(ShardKey<string> shardKey, ShardId shardId, out ShardMap<string> shardMap)
        {
            var added = _dict.TryAdd(shardKey, shardId); shardMap = new(shardKey, _dict[shardKey]); return added;
        }
        public bool TryGetShardIdForKey(ShardKey<string> shardKey, out ShardId shardId) => _dict.TryGetValue(shardKey, out shardId!);
        public bool TryGetOrAdd(ShardKey<string> shardKey, Func<ShardId> valueFactory, out ShardMap<string> shardMap)
        {
            if (_dict.TryGetValue(shardKey, out var existing)) { shardMap = new(shardKey, existing); return false; }
            var id = valueFactory(); _dict[shardKey] = id; shardMap = new(shardKey, id); return true;
        }
        public async IAsyncEnumerable<ShardMap<string>> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var i in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i; await Task.Yield();
            }
        }
    }

    [Fact]
    public void NoOp_When_No_Enumeration_Store()
    {
        var services = new ServiceCollection();
        services.AddShardisMigration<string>();
        services.UseSegmentedEnumerationPlanner<string>(segmentSize: 50); // no enum store registered
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IShardMigrationPlanner<string>>().Should().BeOfType<InMemoryMigrationPlanner<string>>();
    }

    [Fact]
    public void Replaces_When_Enumeration_Store_Present()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IShardMapEnumerationStore<string>>(sp => new EnumStore(Enumerable.Empty<ShardMap<string>>()));
        services.AddShardisMigration<string>();
        services.UseSegmentedEnumerationPlanner<string>(segmentSize: 10);
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IShardMigrationPlanner<string>>().Should().BeOfType<SegmentedEnumerationMigrationPlanner<string>>();
    }
}
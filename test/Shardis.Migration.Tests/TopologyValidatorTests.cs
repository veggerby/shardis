using AwesomeAssertions;
using Shardis.Migration.Topology;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class TopologyValidatorTests
{
    private sealed class FakeEnumStore : IShardMapEnumerationStore<string>
    {
        private readonly List<ShardMap<string>> _items;
        private readonly Dictionary<ShardKey<string>, ShardId> _map;
        public FakeEnumStore(IEnumerable<ShardMap<string>> items)
        {
            _items = items.ToList();
            _map = new Dictionary<ShardKey<string>, ShardId>();
            foreach (var m in _items)
            {
                // keep first assignment only; duplicates remain only in enumeration for validator to detect
                if (!_map.ContainsKey(m.ShardKey))
                {
                    _map[m.ShardKey] = m.ShardId;
                }
            }
        }
        public ShardMap<string> AssignShardToKey(ShardKey<string> shardKey, ShardId shardId)
        {
            _map[shardKey] = shardId;
            return new ShardMap<string>(shardKey, shardId);
        }
        public bool TryAssignShardToKey(ShardKey<string> shardKey, ShardId shardId, out ShardMap<string> shardMap)
        {
            if (_map.ContainsKey(shardKey))
            {
                shardMap = new ShardMap<string>(shardKey, _map[shardKey]);
                return false;
            }
            _map[shardKey] = shardId;
            shardMap = new ShardMap<string>(shardKey, shardId);
            return true;
        }
        public bool TryGetShardIdForKey(ShardKey<string> shardKey, out ShardId shardId) => _map.TryGetValue(shardKey, out shardId!);
        public bool TryGetOrAdd(ShardKey<string> shardKey, Func<ShardId> valueFactory, out ShardMap<string> shardMap)
        {
            if (_map.TryGetValue(shardKey, out var existing))
            {
                shardMap = new ShardMap<string>(shardKey, existing);
                return false;
            }
            var id = valueFactory();
            _map[shardKey] = id;
            shardMap = new ShardMap<string>(shardKey, id);
            return true;
        }
        public IAsyncEnumerable<ShardMap<string>> EnumerateAsync(CancellationToken cancellationToken = default) => EnumerateImpl();
        private async IAsyncEnumerable<ShardMap<string>> EnumerateImpl()
        {
            foreach (var i in _items)
            {
                yield return i;
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task Validate_NoDuplicates()
    {
        // arrange
        var items = Enumerable.Range(0, 10).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("s" + (i % 2))));
        var store = new FakeEnumStore(items);

        // act
        var (total, counts) = await TopologyValidator.ValidateAsync(store);

        // assert
        total.Should().Be(10);
        counts.Values.Sum().Should().Be(10);
    }

    [Fact]
    public async Task Validate_Duplicate_Throws()
    {
        // arrange
        var dup = new ShardMap<string>(new ShardKey<string>("k1"), new ShardId("s0"));
        var items = new[]
        {
            new ShardMap<string>(new ShardKey<string>("k0"), new ShardId("s0")),
            dup,
            dup
        };
        var store = new FakeEnumStore(items);

        // act
        Func<Task> act = () => TopologyValidator.ValidateAsync(store);

        // assert
        await act.Should().ThrowAsync<InvalidOperationException>().Where(e => e.Message.Contains("Duplicate key"));
    }
}

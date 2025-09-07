using Shardis.Factories;
using Shardis.Model;

namespace Shardis.Tests.Factories;

public class ShardFactoryTests
{
    private readonly ShardId _shard = new("s1");

    private sealed class TestResource : IAsyncDisposable, IDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        public void Dispose() { Disposed = true; }
    }

    private sealed class TestFactory : IShardFactory<TestResource>
    {
        public ValueTask<TestResource> CreateAsync(ShardId shard, CancellationToken ct = default) => new(new TestResource());
    }

    [Fact]
    public async Task UseAsync_DisposesAsyncDisposable()
    {
        // arrange
        var factory = new TestFactory();
        TestResource? captured = null;

        // act
        await factory.UseAsync(_shard, (res, _) => { captured = res; return ValueTask.CompletedTask; });

        // assert
        captured.Should().NotBeNull();
        captured!.Disposed.Should().BeTrue();
    }

    [Fact]
    public void InMemoryShardMap_ReturnsConfiguredConnectionString()
    {
        // arrange
        var shard2 = new ShardId("s2");
        var map = new InMemoryShardMap(new Dictionary<ShardId, string>
        {
            { _shard, "cs1" },
            { shard2, "cs2" }
        });

        // act
        var cs = map.GetConnectionString(shard2);

        // assert
        cs.Should().Be("cs2");
    }

    [Fact]
    public void InMemoryShardMap_ThrowsForMissing()
    {
        // arrange
        var map = new InMemoryShardMap(new Dictionary<ShardId, string>());

        // act & assert
        Action act = () => map.GetConnectionString(_shard);
        act.Should().Throw<KeyNotFoundException>();
    }
}
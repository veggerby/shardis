using Microsoft.Extensions.DependencyInjection;

using Shardis.Factories;
using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public sealed class ShardAndFactoryTests
{
    public sealed class TestSession : IAsyncDisposable, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestFactory(bool supportSync)
        : IShardFactory<TestSession>
    {
        public int CreateCalls { get; private set; }
        public int CreateAsyncCalls { get; private set; }

        public TestSession LastSession { get; private set; } = null!;

        public TestSession Create(ShardId shard)
        {
            CreateCalls++;
            if (!supportSync)
            {
                throw new NotSupportedException();
            }
            LastSession = new TestSession();
            return LastSession;
        }

        public ValueTask<TestSession> CreateAsync(ShardId shard, CancellationToken ct = default)
        {
            CreateAsyncCalls++;
            LastSession = new TestSession();
            return new ValueTask<TestSession>(LastSession);
        }
    }

    [Fact]
    public void Shard_ToString_ReturnsId()
    {
        // arrange
        var id = new ShardId("s-1");
        var factory = new TestFactory(supportSync: true);
        var shard = new Shard<TestSession>(id, factory);

        // act
        var s = shard.ToString();

        // assert
        s.Should().Be(id.Value);
    }

    [Fact]
    public async Task Shard_CreateSessionAsync_UsesFactory_And_SyncThrowsWhenUnsupported()
    {
        // arrange
        var id = new ShardId("s-2");
        var factory = new TestFactory(supportSync: false);
        var shard = new Shard<TestSession>(id, factory);

        // act & assert sync
        Action sync = () => shard.CreateSession();
        sync.Should().Throw<NotSupportedException>();

        // act async
        var createdAsync = await shard.CreateSessionAsync();

        // assert async
        createdAsync.Should().NotBeNull();
        factory.CreateAsyncCalls.Should().Be(1);
    }

    [Fact]
    public async Task DelegatingShardFactory_CreateAsync_ReturnsDelegateValue()
    {
        // arrange
        var sid = new ShardId("abc");
        var factory = new DelegatingShardFactory<TestSession>((shard, ct) => new ValueTask<TestSession>(new TestSession()));

        // act
        var result = await factory.CreateAsync(sid);

        // assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ShardFactoryExtensions_UseAsync_DisposesAsyncDisposable()
    {
        // arrange
        var sid = new ShardId("use-1");
        var session = new TestSession();
        var factory = new TestFactory(supportSync: false);
        // force deterministic returned session
        // override factory session by calling CreateAsync once inside UseAsync path

        // act
        TestSession captured = null!;
        await factory.UseAsync(sid, async (s, ct) => { captured = s; await Task.Yield(); });

        // assert
        captured.Should().NotBeNull();
        captured.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Shard_Ctor_Throws_On_InvalidArgs()
    {
        // arrange
        var id = new ShardId("good");
        var factory = new TestFactory(supportSync: false);

        // act/assert
        Action ctor1 = () => _ = new Shard<TestSession>(new ShardId(""), factory);
        Action ctor2 = () => _ = new Shard<TestSession>(id, null!);
        ctor1.Should().Throw<ArgumentException>();
        ctor2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddShardis_Throws_When_No_Shards()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        Action act = () => services.AddShardis<SimpleShard, string, string>(opts => { /* no shards */ });

        // assert
        act.Should().Throw<ShardisException>();
    }

    [Fact]
    public void AddShardis_Registers_DefaultRouter_When_NotConsistent()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShardMapStore<string>, InMemoryShardMapStore<string>>();
        services.AddSingleton(DefaultShardKeyHasher<string>.Instance);

        services.AddShardis<SimpleShard, string, string>(opts =>
        {
            opts.UseConsistentHashing = false; // force default router path
            opts.Shards.Add(new SimpleShard(new("sh-1"), "c1"));
        });

        // act
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IShardRouter<string, string>>();

        // assert
        router.Should().BeOfType<DefaultShardRouter<string, string>>();
    }

    [Fact]
    public void AddShardis_Registers_ConsistentRouter_When_Enabled()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShardMapStore<string>, InMemoryShardMapStore<string>>();
        services.AddSingleton(DefaultShardKeyHasher<string>.Instance);

        services.AddShardis<SimpleShard, string, string>(opts =>
        {
            opts.UseConsistentHashing = true;
            opts.Shards.Add(new SimpleShard(new("sh-1"), "c1"));
            opts.Shards.Add(new SimpleShard(new("sh-2"), "c2"));
        });

        // act
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IShardRouter<string, string>>();

        // assert
        router.Should().BeOfType<ConsistentHashShardRouter<SimpleShard, string, string>>();
    }
}
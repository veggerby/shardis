using Shardis.Model;
using Shardis.Redis;

namespace Shardis.Tests;

/// <summary>
/// Integration tests for RedisShardMapStore using Testcontainers.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RedisShardMapStoreIntegrationTests : IClassFixture<RedisContainerFixture>
{
    private readonly RedisContainerFixture _fixture;

    public RedisShardMapStoreIntegrationTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AssignShardToKeyAsync_CanStoreAndRetrieveKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-123");
        var shard = new ShardId("shard-1");

        // act
        var map = await store.AssignShardToKeyAsync(key, shard);
        var retrieved = await store.TryGetShardIdForKeyAsync(key);

        // assert
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard);
        retrieved.Should().NotBeNull();
        retrieved!.Value.Should().Be(shard);
    }

    [Fact]
    public async Task TryAssignShardToKeyAsync_ReturnsCreatedTrueForNewKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-456");
        var shard = new ShardId("shard-2");

        // act
        var (created, map) = await store.TryAssignShardToKeyAsync(key, shard);

        // assert
        created.Should().BeTrue();
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard);
    }

    [Fact]
    public async Task TryAssignShardToKeyAsync_ReturnsCreatedFalseForExistingKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-789");
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");

        // act
        await store.AssignShardToKeyAsync(key, shard1);
        var (created, map) = await store.TryAssignShardToKeyAsync(key, shard2);

        // assert
        created.Should().BeFalse();
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard1); // original shard preserved
    }

    [Fact]
    public async Task TryGetOrAddAsync_CreatesNewAssignmentWhenKeyDoesNotExist()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-new");
        var shard = new ShardId("shard-3");

        // act
        var (created, map) = await store.TryGetOrAddAsync(key, () => shard);

        // assert
        created.Should().BeTrue();
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard);
    }

    [Fact]
    public async Task TryGetOrAddAsync_ReturnsExistingAssignmentWhenKeyExists()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-existing");
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");
        await store.AssignShardToKeyAsync(key, shard1);

        // act
        var (created, map) = await store.TryGetOrAddAsync(key, () => shard2);

        // assert
        created.Should().BeFalse();
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard1); // original shard preserved
    }

    [Fact]
    public async Task TryGetShardIdForKeyAsync_ReturnsNullForNonExistentKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("non-existent-key");

        // act
        var result = await store.TryGetShardIdForKeyAsync(key);

        // assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetShardIdForKey_SyncVersion_CanRetrieveKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-sync");
        var shard = new ShardId("shard-4");
        store.AssignShardToKey(key, shard);

        // act
        var found = store.TryGetShardIdForKey(key, out var retrievedShard);

        // assert
        found.Should().BeTrue();
        retrievedShard.Should().Be(shard);
    }

    [Fact]
    public void TryGetShardIdForKey_ReturnsFalseForNonExistentKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("non-existent-sync");

        // act
        var found = store.TryGetShardIdForKey(key, out var retrievedShard);

        // assert
        found.Should().BeFalse();
        retrievedShard.Should().Be(default(ShardId));
    }

    [Fact]
    public void TryAssignShardToKey_SyncVersion_ReturnsCreatedTrueForNewKey()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-sync-new");
        var shard = new ShardId("shard-5");

        // act
        var created = store.TryAssignShardToKey(key, shard, out var map);

        // assert
        created.Should().BeTrue();
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard);
    }

    [Fact]
    public void TryGetOrAdd_SyncVersion_CreatesNewAssignmentWhenKeyDoesNotExist()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        var key = new ShardKey<string>("user-sync-add");
        var shard = new ShardId("shard-6");

        // act
        var created = store.TryGetOrAdd(key, () => shard, out var map);

        // assert
        created.Should().BeTrue();
        map.ShardKey.Should().Be(key);
        map.ShardId.Should().Be(shard);
    }
}

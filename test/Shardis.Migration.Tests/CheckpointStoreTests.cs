using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class CheckpointStoreTests
{
    [Fact]
    public async Task Can_Roundtrip_Checkpoint()
    {
        // arrange
        var store = new InMemoryCheckpointStore<string>();
        var planId = Guid.NewGuid();
        var key = new ShardKey<string>("k1");
        var states = new Dictionary<ShardKey<string>, KeyMoveState> { [key] = KeyMoveState.Copied };
        var cp = new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow, states, 0);

        // act
        await store.PersistAsync(cp, CancellationToken.None);
        var loaded = await store.LoadAsync(planId, CancellationToken.None);

        // assert
        loaded.Should().NotBeNull();
        loaded!.States[key].Should().Be(KeyMoveState.Copied);
    }

    [Fact]
    public async Task Concurrent_Persists_Last_Write_Wins()
    {
        // arrange
        var store = new InMemoryCheckpointStore<string>();
        var planId = Guid.NewGuid();
        var key = new ShardKey<string>("k1");
        var states1 = new Dictionary<ShardKey<string>, KeyMoveState> { [key] = KeyMoveState.Copied };
        var states2 = new Dictionary<ShardKey<string>, KeyMoveState> { [key] = KeyMoveState.Verified };
        var cp1 = new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow, states1, 0);
        var cp2 = new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow.AddSeconds(1), states2, 1);

        // act
        await Task.WhenAll(
            Task.Run(() => store.PersistAsync(cp1, CancellationToken.None)),
            Task.Run(() => store.PersistAsync(cp2, CancellationToken.None)));
        var loaded = await store.LoadAsync(planId, CancellationToken.None);

        // assert
        loaded.Should().NotBeNull();
        // last writer wins (unordered concurrency, but both write same key so final state must be one of them; assert contains either Copied or Verified and at least one state)
        (loaded!.States[key] == KeyMoveState.Copied || loaded.States[key] == KeyMoveState.Verified).Should().BeTrue();
    }

    [Fact]
    public async Task Load_NonExisting_Returns_Null()
    {
        // arrange
        var store = new InMemoryCheckpointStore<string>();

        // act
        var loaded = await store.LoadAsync(Guid.NewGuid(), CancellationToken.None);

        // assert
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Persist_DefensiveCopy_Protected_From_External_Mutation()
    {
        // arrange
        var store = new InMemoryCheckpointStore<string>();
        var planId = Guid.NewGuid();
        var key = new ShardKey<string>("k1");
        var states = new Dictionary<ShardKey<string>, KeyMoveState> { [key] = KeyMoveState.Copied };
        var cp = new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow, states, 0);
        await store.PersistAsync(cp, CancellationToken.None);

        // act
        states[key] = KeyMoveState.Failed; // mutate original dictionary (should not affect stored checkpoint)
        var loaded = await store.LoadAsync(planId, CancellationToken.None);

        // assert
        loaded!.States[key].Should().Be(KeyMoveState.Copied);
    }

    [Fact]
    public async Task Overwrite_Persist_Replaces_Previous_Checkpoint()
    {
        // arrange
        var store = new InMemoryCheckpointStore<string>();
        var planId = Guid.NewGuid();
        var key = new ShardKey<string>("k1");
        var cp1 = new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow, new Dictionary<ShardKey<string>, KeyMoveState> { [key] = KeyMoveState.Copied }, 0);
        var cp2 = new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow.AddSeconds(1), new Dictionary<ShardKey<string>, KeyMoveState> { [key] = KeyMoveState.Verified }, 1);

        // act
        await store.PersistAsync(cp1, CancellationToken.None);
        await store.PersistAsync(cp2, CancellationToken.None);
        var loaded = await store.LoadAsync(planId, CancellationToken.None);

        // assert
        loaded!.LastProcessedIndex.Should().Be(1);
        loaded.States[key].Should().Be(KeyMoveState.Verified);
    }
}
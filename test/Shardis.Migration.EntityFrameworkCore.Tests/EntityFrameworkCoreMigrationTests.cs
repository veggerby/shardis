using System.Collections.Concurrent;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Migration.Abstractions;
using Shardis.Migration.EntityFrameworkCore;
using Shardis.Migration.EntityFrameworkCore.Verification;
using Shardis.Migration.Execution;
using Shardis.Migration.Model;
using Shardis.Model;
using Shardis.TestUtilities;

namespace Shardis.Migration.EntityFrameworkCore.Tests;

public class EntityFrameworkCoreMigrationTests
{
    // NOTE: Using real rowversion requires provider support; Sqlite lacks it so we simulate by updating a byte[] manually.
    private sealed class Item : IShardEntity<int>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }
        public int Key => Id;
    }

    private sealed class ItemContext(DbContextOptions<ItemContext> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>().HasKey(i => i.Id);
            // Intentionally NOT marking RowVersion as IsRowVersion for Sqlite tests to allow direct byte propagation between shards.
            modelBuilder.Entity<Item>().Property(i => i.RowVersion);
        }
    }


    private sealed class InMemoryMetrics : IShardMigrationMetrics
    {
        public long Planned; public long Copied; public long Verified; public long Swapped; public long Failed; public long Retries; public int ActiveCopy; public int ActiveVerify;
        public void IncPlanned(long delta = 1) { Planned += delta; }
        public void IncCopied(long delta = 1) { Copied += delta; }
        public void IncVerified(long delta = 1) { Verified += delta; }
        public void IncSwapped(long delta = 1) { Swapped += delta; }
        public void IncFailed(long delta = 1) { Failed += delta; }
        public void IncRetries(long delta = 1) { Retries += delta; }
        public void SetActiveCopy(int value) { ActiveCopy = value; }
        public void SetActiveVerify(int value) { ActiveVerify = value; }
    }

    private sealed class InMemoryCheckpointStore<TKey> : IShardMigrationCheckpointStore<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly Dictionary<Guid, MigrationCheckpoint<TKey>> _store = new();
        public Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct)
        {
            _store.TryGetValue(planId, out var cp);
            return Task.FromResult(cp);
        }
        public Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
        {
            _store[checkpoint.PlanId] = checkpoint;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMapSwapper<TKey>(Dictionary<ShardKey<TKey>, ShardId> map, bool flaky = false, int failCount = 0) : IShardMapSwapper<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly Dictionary<ShardKey<TKey>, ShardId> _map = map;
        private readonly bool _flaky = flaky; private int _remaining = failCount;
        public List<IReadOnlyList<KeyMove<TKey>>> Batches { get; } = new();
        public Task SwapAsync(IReadOnlyList<KeyMove<TKey>> moves, CancellationToken ct)
        {
            if (_flaky && _remaining-- > 0) { throw new InvalidOperationException("swap transient"); }
            foreach (var m in moves) { _map[m.Key] = m.Target; }
            Batches.Add(moves);
            return Task.CompletedTask;
        }
    }

    private static byte[] NextVersion(int v) => BitConverter.GetBytes(v);

    private static void SeedShard(ItemContext ctx, params Item[] items)
    {
        if (!ctx.Items.Any())
        {
            foreach (var it in items)
            {
                it.RowVersion ??= NextVersion(1);
                ctx.Items.Add(it);
            }
            ctx.SaveChanges();
        }
    }

    private static ShardMigrationExecutor<int> CreateExecutor(
        IShardDbContextFactory<ItemContext> factory,
        IVerificationStrategy<int> verification,
        IShardMapSwapper<int> swapper,
    ShardMigrationOptions? opts = null,
    IEntityProjectionStrategy? projection = null)
    {
        var mover = new EntityFrameworkCoreDataMover<int, ItemContext, Item>(factory, projection ?? new NoOpEntityProjectionStrategy());
        var metrics = new InMemoryMetrics();
        var checkpoint = new InMemoryCheckpointStore<int>();
        opts ??= new ShardMigrationOptions
        {
            CopyConcurrency = 4,
            VerifyConcurrency = 4,
            InterleaveCopyAndVerify = false,
            SwapBatchSize = 8,
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(10)
        };
        return new ShardMigrationExecutor<int>(mover, verification, swapper, checkpoint, metrics, opts);
    }

    [Fact]
    public async Task HappyPath_RowVersionVerification_CopiesAndSwaps()
    {
        // arrange
        var shardMap = new Dictionary<ShardKey<int>, ShardId>();
        var source = new ShardId("s1"); var target = new ShardId("s2");
        var factory = new SqliteShardDbContextFactory<ItemContext>(opts => new ItemContext(opts));
        // seed
        using (var sctx = await factory.CreateAsync(source)) { SeedShard(sctx, new Item { Id = 1, Name = "Alpha" }); }
        using (var tctx = await factory.CreateAsync(target)) { SeedShard(tctx); }
        var verification = new RowVersionVerificationStrategy<int, ItemContext, Item>(factory);
        var swapper = new RecordingMapSwapper<int>(shardMap);
        var executor = CreateExecutor(factory, verification, swapper, opts: new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, InterleaveCopyAndVerify = false, SwapBatchSize = 8, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(10), ForceSwapOnVerificationFailure = false });
        var plan = new MigrationPlan<int>(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { new KeyMove<int>(new ShardKey<int>(1), source, target) });
        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        summary.Done.Should().Be(1);
        shardMap[new ShardKey<int>(1)].Should().Be(target);
        using var verifyCtx = await factory.CreateAsync(target); (await verifyCtx.Items.FindAsync(1))!.Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task MissingSourceEntity_VerificationFails_NoSwap()
    {
        // arrange
        var shardMap = new Dictionary<ShardKey<int>, ShardId>();
        var source = new ShardId("s1"); var target = new ShardId("s2");
        var factory = new SqliteShardDbContextFactory<ItemContext>(opts => new ItemContext(opts));
        using (var tctx = await factory.CreateAsync(target)) { SeedShard(tctx); }
        var verification = new RowVersionVerificationStrategy<int, ItemContext, Item>(factory);
        var swapper = new RecordingMapSwapper<int>(shardMap);
        var executor = CreateExecutor(factory, verification, swapper, opts: new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, InterleaveCopyAndVerify = false, SwapBatchSize = 8, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(10), ForceSwapOnVerificationFailure = false });
        var plan = new MigrationPlan<int>(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { new KeyMove<int>(new ShardKey<int>(42), source, target) });
        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        summary.Done.Should().Be(0);
        summary.Failed.Should().Be(1); // verify failed
        shardMap.ContainsKey(new ShardKey<int>(42)).Should().BeFalse();
    }

    [Fact]
    public async Task Idempotent_Rerun_SecondRunDoesNotDuplicateSwap()
    {
        // arrange
        var shardMap = new Dictionary<ShardKey<int>, ShardId>();
        var source = new ShardId("s1"); var target = new ShardId("s2");
        var factory = new SqliteShardDbContextFactory<ItemContext>(opts => new ItemContext(opts));
        using (var sctx = await factory.CreateAsync(source)) { SeedShard(sctx, new Item { Id = 7, Name = "Seven" }); }
        using (var tctx = await factory.CreateAsync(target)) { SeedShard(tctx); }
        var verification = new RowVersionVerificationStrategy<int, ItemContext, Item>(factory);
        var swapper = new RecordingMapSwapper<int>(shardMap);
        var executor = CreateExecutor(factory, verification, swapper, opts: new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, InterleaveCopyAndVerify = false, SwapBatchSize = 8, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(10), ForceSwapOnVerificationFailure = false });
        var plan = new MigrationPlan<int>(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { new KeyMove<int>(new ShardKey<int>(7), source, target) });

        // act
        await executor.ExecuteAsync(plan, null, CancellationToken.None);
        var second = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        second.Done.Should().Be(1);
        swapper.Batches.Count.Should().Be(1); // only one swap batch executed
    }

    [Fact]
    public async Task Swap_Retry_OnTransientFailure_Succeeds()
    {
        // arrange
        var shardMap = new Dictionary<ShardKey<int>, ShardId>();
        var source = new ShardId("s1"); var target = new ShardId("s2");
        var factory = new SqliteShardDbContextFactory<ItemContext>(opts => new ItemContext(opts));
        using (var sctx = await factory.CreateAsync(source)) { SeedShard(sctx, new Item { Id = 9, Name = "Nine" }); }
        using (var tctx = await factory.CreateAsync(target)) { SeedShard(tctx); }
        var verification = new RowVersionVerificationStrategy<int, ItemContext, Item>(factory);
        var swapper = new RecordingMapSwapper<int>(shardMap, flaky: true, failCount: 1);
        var executor = CreateExecutor(factory, verification, swapper, opts: new ShardMigrationOptions { CopyConcurrency = 4, VerifyConcurrency = 4, InterleaveCopyAndVerify = false, SwapBatchSize = 8, MaxRetries = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(10), ForceSwapOnVerificationFailure = false });
        var plan = new MigrationPlan<int>(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { new KeyMove<int>(new ShardKey<int>(9), source, target) });

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        summary.Done.Should().Be(1);
        swapper.Batches.Count.Should().Be(1);
    }

    [Fact]
    public async Task ChecksumVerification_DetectsContentChange()
    {
        // arrange
        var shardMap = new Dictionary<ShardKey<int>, ShardId>();
        var source = new ShardId("s1"); var target = new ShardId("s2");
        var factory = new SqliteShardDbContextFactory<ItemContext>(opts => new ItemContext(opts));
        using (var sctx = await factory.CreateAsync(source)) { SeedShard(sctx, new Item { Id = 11, Name = "Orig" }); }
        using (var tctx = await factory.CreateAsync(target)) { SeedShard(tctx, new Item { Id = 11, Name = "Different" }); }
        var canonicalizer = new JsonStableCanonicalizer();
        var hasher = new Fnv1a64Hasher();
        var checksum = new ChecksumVerificationStrategy<int, ItemContext, Item>(factory, canonicalizer, hasher);
        // Projection mutates the copied entity to force checksum mismatch between source and target.
        var mutationProjection = new MutationProjectionStrategy();
        var swapper = new RecordingMapSwapper<int>(shardMap);
        var executor = CreateExecutor(factory, checksum, swapper, projection: mutationProjection);
        var plan = new MigrationPlan<int>(Guid.NewGuid(), DateTimeOffset.UtcNow, new[] { new KeyMove<int>(new ShardKey<int>(11), source, target) });

        // act
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // assert
        summary.Done.Should().Be(0);
        summary.Failed.Should().Be(1);
        shardMap.ContainsKey(new ShardKey<int>(11)).Should().BeFalse();
    }
    private sealed class MutationProjectionStrategy : IEntityProjectionStrategy
    {
        public TTarget Project<TSource, TTarget>(TSource source, ProjectionContext context) where TSource : class where TTarget : class
        {
            if (source is Item s && typeof(TTarget) == typeof(Item))
            {
                var mutated = new Item { Id = s.Id, Name = s.Name + "_MUT", RowVersion = s.RowVersion };
                return (TTarget)(object)mutated;
            }
            return source as TTarget ?? throw new InvalidOperationException("Unsupported projection type");
        }
    }
}
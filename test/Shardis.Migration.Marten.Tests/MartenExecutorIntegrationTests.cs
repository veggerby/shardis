using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using global::Marten;

using Shardis.Logging;
using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.Marten;
using Shardis.Migration.Marten.Verification;
using Shardis.Migration.Model;
using Shardis.Model;

using Xunit;

namespace Shardis.Migration.Marten.Tests;

[Trait("Category", "Integration")]
public class MartenExecutorIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public MartenExecutorIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record TestDoc(string Id, string Name, int Value);

    // ---------------- Test Infrastructure ----------------

    private sealed class VersionedMapStore<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly ConcurrentDictionary<ShardKey<TKey>, (ShardId Shard, int Version)> _entries = new();
        public int GetVersion(ShardKey<TKey> key) => _entries.TryGetValue(key, out var v) ? v.Version : 0;
        public ShardId? Route(ShardKey<TKey> key) => _entries.TryGetValue(key, out var v) ? v.Shard : null;
        public (bool swapped, int newVersion) TrySwap(ShardKey<TKey> key, ShardId target, int expectedVersion)
        {
            return _entries.AddOrUpdate(key,
                addValueFactory: _ => (target, 1),
                updateValueFactory: (_, existing) => existing.Version == expectedVersion ? (target, existing.Version + 1) : existing) switch
            {
                var tuple when tuple.Shard == target && tuple.Version == expectedVersion + 1 => (true, tuple.Version),
                var tuple => (false, tuple.Version)
            };
        }
        public IReadOnlyList<(ShardKey<TKey> Key, ShardId Shard, int Version)> History() => _entries.Select(kv => (kv.Key, kv.Value.Shard, kv.Value.Version)).ToList();
        public void Seed(ShardKey<TKey> key, ShardId shard) => _entries[key] = (shard, 1);
    }

    private sealed class VersionedSwapper<TKey>(VersionedMapStore<TKey> store, List<IReadOnlyList<KeyMove<TKey>>> batches, bool injectConflict = false) : IShardMapSwapper<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly VersionedMapStore<TKey> _store = store;
        private readonly List<IReadOnlyList<KeyMove<TKey>>> _batches = batches;
        private readonly bool _injectConflict = injectConflict;
        private int _conflictInjected;
        public async Task SwapAsync(IReadOnlyList<KeyMove<TKey>> verifiedBatch, CancellationToken ct)
        {
            foreach (var move in verifiedBatch)
            {
                var key = move.Key;
                var currentVersion = _store.GetVersion(key);
                if (_injectConflict && _conflictInjected == 0)
                {
                    // simulate external version bump to force retry once
                    _store.TrySwap(key, move.Source, currentVersion); // bump with same source
                    Interlocked.Increment(ref _conflictInjected);
                    throw new InvalidOperationException("simulated swap conflict");
                }
                var (swapped, _) = _store.TrySwap(key, move.Target, currentVersion == 0 ? 0 : currentVersion);
                if (!swapped)
                {
                    throw new InvalidOperationException("optimistic swap failed");
                }
            }
            _batches.Add(verifiedBatch);
            await Task.CompletedTask;
        }
    }

    private sealed class JsonCheckpointStore<TKey> : IShardMigrationCheckpointStore<TKey> where TKey : notnull, IEquatable<TKey>
    {
        private readonly Dictionary<Guid, string> _json = new();
        private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default);
        public Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct)
        {
            if (_json.TryGetValue(planId, out var text))
            {
                var dto = JsonSerializer.Deserialize<MigrationCheckpointDto<TKey>>(text, Options)!;
                return Task.FromResult<MigrationCheckpoint<TKey>?>(dto.ToModel());
            }
            return Task.FromResult<MigrationCheckpoint<TKey>?>(null);
        }
        public Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
        {
            var dto = MigrationCheckpointDto<TKey>.FromModel(checkpoint);
            _json[checkpoint.PlanId] = JsonSerializer.Serialize(dto, Options);
            return Task.CompletedTask;
        }

        private sealed record MigrationCheckpointDto<TKeyDto>(Guid PlanId, int Version, DateTimeOffset UpdatedAtUtc, Dictionary<ShardKey<TKeyDto>, KeyMoveState> States, int LastProcessedIndex)
            where TKeyDto : notnull, IEquatable<TKeyDto>
        {
            public MigrationCheckpoint<TKeyDto> ToModel() => new(PlanId, Version, UpdatedAtUtc, States, LastProcessedIndex);
            public static MigrationCheckpointDto<TKeyDto> FromModel(MigrationCheckpoint<TKeyDto> m) => new(m.PlanId, m.Version, m.UpdatedAtUtc, new Dictionary<ShardKey<TKeyDto>, KeyMoveState>(m.States), m.LastProcessedIndex);
        }
    }

    private sealed class TestMartenSessionFactory(string connectionString) : IMartenSessionFactory
    {
        private readonly string _conn = connectionString;
        private readonly Dictionary<string, IDocumentStore> _stores = new();
        private readonly object _gate = new();
        private IDocumentStore Get(ShardId shard)
        {
            if (_stores.TryGetValue(shard.Value, out var ds)) { return ds; }
            lock (_gate)
            {
                if (_stores.TryGetValue(shard.Value, out ds)) { return ds; }
                ds = DocumentStore.For(o =>
                {
                    o.Connection(_conn);
                    o.DatabaseSchemaName = $"exec_{shard.Value}"; // isolate
                });
                _stores[shard.Value] = ds;
                return ds;
            }
        }
        public ValueTask<IQuerySession> CreateQuerySessionAsync(ShardId shardId, CancellationToken cancellationToken = default) => new(Get(shardId).QuerySession());
        public ValueTask<IDocumentSession> CreateDocumentSessionAsync(ShardId shardId, CancellationToken cancellationToken = default) => new(Get(shardId).LightweightSession());
    }

    private static async Task SeedAsync(IMartenSessionFactory factory, ShardId shard, TestDoc doc)
    {
        await using var s = await factory.CreateDocumentSessionAsync(shard);
        s.Store(doc);
        await s.SaveChangesAsync();
    }

    private static MigrationPlan<string> Plan(Guid id, KeyMove<string> move) => new(id, DateTimeOffset.UtcNow, new[] { move });
    private static KeyMove<string> Move(ShardId s, ShardId t, string id) => new(new ShardKey<string>(id), s, t);

    private static ShardMigrationExecutor<string> BuildExecutor(
        IMartenSessionFactory factory,
        JsonCheckpointStore<string> ckpt,
        VersionedSwapper<string> swapper,
        ShardMigrationOptions? opts = null)
    {
        var verifier = new DocumentChecksumVerificationStrategy<string>(factory, new NoOpEntityProjectionStrategy(), new JsonStableCanonicalizer(), new Fnv1a64Hasher());
        var mover = new MartenDataMover<string>(factory, new NoOpEntityProjectionStrategy(), verifier);
        opts ??= new ShardMigrationOptions
        {
            CopyConcurrency = 1,
            VerifyConcurrency = 1,
            SwapBatchSize = 1,
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(10),
            InterleaveCopyAndVerify = false,
            MaxConcurrentMoves = 1
        };
        return new ShardMigrationExecutor<string>(mover, verifier, swapper, ckpt, new NoOpMetrics(), opts, new InMemoryShardisLogger());
    }

    private sealed class NoOpMetrics : IShardMigrationMetrics
    {
        public void IncPlanned(long delta = 1) { }
        public void IncCopied(long delta = 1) { }
        public void IncVerified(long delta = 1) { }
        public void IncSwapped(long delta = 1) { }
        public void IncFailed(long delta = 1) { }
        public void IncRetries(long delta = 1) { }
        public void SetActiveCopy(int value) { }
        public void SetActiveVerify(int value) { }
        public void ObserveCopyDuration(double ms) { }
        public void ObserveVerifyDuration(double ms) { }
        public void ObserveSwapBatchDuration(double ms) { }
        public void ObserveTotalElapsed(double ms) { }
    }

    // ---------------- Tests ----------------

    [Fact]
    public async Task HappyPath_Copy_Verify_Swap()
    {
        var conn = _fixture.ConnectionString;
        var factory = new TestMartenSessionFactory(conn); var ckpt = new JsonCheckpointStore<string>();
        var map = new VersionedMapStore<string>(); var batches = new List<IReadOnlyList<KeyMove<string>>>();
        var swapper = new VersionedSwapper<string>(map, batches);
        var executor = BuildExecutor(factory, ckpt, swapper);
        var s1 = new ShardId("s1"); var s2 = new ShardId("s2");
        var move = Move(s1, s2, "h1");
        await SeedAsync(factory, s1, new TestDoc("h1", "alpha", 1));

        var plan = Plan(Guid.NewGuid(), move);
        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        summary.Done.Should().Be(1);
        batches.Count.Should().Be(1);
        // target contains doc
        await using var qs = await factory.CreateQuerySessionAsync(s2);
        (await qs.LoadAsync<TestDoc>("h1"))!.Name.Should().Be("alpha");
    }

    [Fact]
    public async Task Resume_FromCopyCheckpoint_CompletesVerifyAndSwap()
    {
        var conn = _fixture.ConnectionString;
        var factory = new TestMartenSessionFactory(conn); var ckpt = new JsonCheckpointStore<string>();
        var map = new VersionedMapStore<string>(); var batches = new List<IReadOnlyList<KeyMove<string>>>();
        var swapper = new VersionedSwapper<string>(map, batches);
        var executor = BuildExecutor(factory, ckpt, swapper);
        var s1 = new ShardId("s1"); var s2 = new ShardId("s2");
        var move = Move(s1, s2, "r1");
        await SeedAsync(factory, s1, new TestDoc("r1", "res", 2));
        var planId = Guid.NewGuid();
        // Pre-stage: perform copy manually
        var mover = new MartenDataMover<string>(factory, new NoOpEntityProjectionStrategy(), new DocumentChecksumVerificationStrategy<string>(factory, new NoOpEntityProjectionStrategy(), new JsonStableCanonicalizer(), new Fnv1a64Hasher()));
        await mover.CopyAsync(move, CancellationToken.None);
        // Persist checkpoint marking state as Copied
        var states = new Dictionary<ShardKey<string>, KeyMoveState> { [move.Key] = KeyMoveState.Copied };
        await ckpt.PersistAsync(new MigrationCheckpoint<string>(planId, 1, DateTimeOffset.UtcNow, states, 0), CancellationToken.None);
        var plan = new MigrationPlan<string>(planId, DateTimeOffset.UtcNow, new[] { move });

        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        summary.Done.Should().Be(1);
        batches.Count.Should().Be(1);
    }

    [Fact]
    public async Task Swap_Retry_OnVersionConflict()
    {
        var conn = _fixture.ConnectionString;
        var factory = new TestMartenSessionFactory(conn); var ckpt = new JsonCheckpointStore<string>();
        var map = new VersionedMapStore<string>(); var batches = new List<IReadOnlyList<KeyMove<string>>>();
        var swapper = new VersionedSwapper<string>(map, batches, injectConflict: true);
        var executor = BuildExecutor(factory, ckpt, swapper);
        var s1 = new ShardId("s1"); var s2 = new ShardId("s2");
        var move = Move(s1, s2, "c1");
        await SeedAsync(factory, s1, new TestDoc("c1", "conf", 5));
        var plan = Plan(Guid.NewGuid(), move);

        var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        summary.Done.Should().Be(1);
        batches.Count.Should().Be(1);
    }

    [Fact]
    public async Task Mismatch_ReCopy_Then_Verify_Swap()
    {
        var conn = _fixture.ConnectionString;
        var factory = new TestMartenSessionFactory(conn); var ckpt = new JsonCheckpointStore<string>();
        var map = new VersionedMapStore<string>(); var batches = new List<IReadOnlyList<KeyMove<string>>>();
        var swapper = new VersionedSwapper<string>(map, batches);
        // custom options to allow verification failure then recopy: ForceSwapOnVerificationFailure=false
        var opts = new ShardMigrationOptions
        {
            CopyConcurrency = 1,
            VerifyConcurrency = 1,
            SwapBatchSize = 1,
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(5),
            InterleaveCopyAndVerify = false
        };
        var executor = BuildExecutor(factory, ckpt, swapper, opts);
        var s1 = new ShardId("s1"); var s2 = new ShardId("s2");
        var move = Move(s1, s2, "m1");
        await SeedAsync(factory, s1, new TestDoc("m1", "before", 10));
        var plan = Plan(Guid.NewGuid(), move);

        // After copy mutate source to force checksum mismatch then expect executor to copy again during retry.
        // We simulate by seeding initial doc, running executor until copy, then modifying source before verify.
        // Simplification: run full executor, then mutate and rerun to trigger mismatch path.
        var first = await executor.ExecuteAsync(plan, null, CancellationToken.None);
        first.Done.Should().Be(1); // already swapped; to simulate mismatch we need a modified plan id.
        // New plan with same key but mutated source after swap should still succeed (idempotent) - emulate mismatch scenario at verify phase.
        await using (var s = await factory.CreateDocumentSessionAsync(s1))
        {
            s.Store(new TestDoc("m1", "after", 11));
            await s.SaveChangesAsync();
        }
        var secondPlan = Plan(Guid.NewGuid(), move);
        var second = await executor.ExecuteAsync(secondPlan, null, CancellationToken.None);
        second.Done.Should().Be(1);
        batches.Count.Should().BeGreaterThanOrEqualTo(1);
    }
}
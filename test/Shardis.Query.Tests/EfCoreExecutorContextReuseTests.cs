using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Query.Tests;

public sealed class EfCoreExecutorContextReuseTests
{
    private sealed class Dummy
    {
        public int Id { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Dummy> Dummies => Set<Dummy>();
    }

    private sealed class TrackingFactory : IShardFactory<DbContext>
    {
        private readonly ConcurrentDictionary<string, int> _creates = new();
        private readonly bool _newGuidDb; // ensure separation when needed

        public TrackingFactory(bool newGuidDb = true)
        {
            _newGuidDb = newGuidDb;
        }

        public IReadOnlyDictionary<string, int> Creates => _creates;

        public ValueTask<DbContext> CreateAsync(ShardId shardId, CancellationToken ct = default)
        {
            _creates.AddOrUpdate(shardId.Value, 1, (_, v) => v + 1);
            var name = _newGuidDb ? $"reuse-{shardId.Value}-{Guid.NewGuid()}" : $"reuse-{shardId.Value}";
            var opts = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(name).Options;
            DbContext ctx = new TestDbContext(opts);
            ctx.Set<Dummy>();
            return new ValueTask<DbContext>(ctx);
        }
    }

    private static QueryModel Model<T>() => QueryModel.Create(typeof(T));

    [Fact]
    public async Task Retained_Context_Per_Shard_When_Dispose_False()
    {
        // arrange
        int shardCount = 4;
        var factory = new TrackingFactory();
        var exec = new EntityFrameworkCoreShardQueryExecutor(
            shardCount,
            factory,
            (streams, ct) => UnorderedMergeHelper.Merge(streams, ct),
            metrics: null,
            commandTimeoutSeconds: null,
            maxConcurrency: null,
            disposeContextPerQuery: false);

        var model = Model<Dummy>();

        // act (enumerate twice)
        await foreach (var _ in exec.ExecuteAsync<Dummy>(model)) { }
        await foreach (var _ in exec.ExecuteAsync<Dummy>(model)) { }

        // assert: only one creation per shard
        factory.Creates.Count.Should().Be(shardCount);
        factory.Creates.Values.Should().OnlyContain(v => v == 1);
    }

    [Fact]
    public async Task New_Context_Per_Enumeration_When_Dispose_True()
    {
        // arrange
        int shardCount = 3;
        var factory = new TrackingFactory();
        var exec = new EntityFrameworkCoreShardQueryExecutor(
            shardCount,
            factory,
            (streams, ct) => UnorderedMergeHelper.Merge(streams, ct),
            metrics: null,
            commandTimeoutSeconds: null,
            maxConcurrency: null,
            disposeContextPerQuery: true);
        var model = Model<Dummy>();

        // act
        await foreach (var _ in exec.ExecuteAsync<Dummy>(model)) { }
        await foreach (var _ in exec.ExecuteAsync<Dummy>(model)) { }

        // assert: two creations per shard (one per enumeration)
        factory.Creates.Count.Should().Be(shardCount);
        factory.Creates.Values.Should().OnlyContain(v => v == 2);
    }
}
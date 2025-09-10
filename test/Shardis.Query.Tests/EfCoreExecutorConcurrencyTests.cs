using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Query.Tests;

public sealed class EfCoreExecutorConcurrencyTests
{
    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Dummy> Dummies => Set<Dummy>();
    }

    private sealed class Dummy
    {
        public int Id { get; set; }
    }

    private sealed class Factory(int delayMs) : IShardFactory<DbContext>
    {
        private readonly int _delay = delayMs;
        private int _current;
        private int _peak;
        public int Peak => _peak;

        public async ValueTask<DbContext> CreateAsync(ShardId shardId, CancellationToken ct = default)
        {
            var now = Interlocked.Increment(ref _current);
            // track peak
            int observed;
            do
            {
                observed = _peak;
                if (now <= observed) break;
            } while (Interlocked.CompareExchange(ref _peak, now, observed) != observed);

            try
            {
                await Task.Delay(_delay, ct).ConfigureAwait(false);
                var opts = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("shard-" + shardId.Value + Guid.NewGuid()).Options;
                var ctx = new TestDbContext(opts);
                // ensure model has DbSet so Set<Dummy>() works
                ctx.Set<Dummy>();
                return ctx;
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }
    }

    private static QueryModel Model<T>() => QueryModel.Create(typeof(T));

    [Fact]
    public async Task Concurrency_Is_Limited()
    {
        // arrange
        int shardCount = 8;
        int maxConc = 2;
        var factory = new Factory(delayMs: 50);

        var exec = new EntityFrameworkCoreShardQueryExecutor(
            shardCount,
            factory,
            (streams, ct) => UnorderedMergeHelper.Merge(streams, ct),
            metrics: null,
            commandTimeoutSeconds: null,
            maxConcurrency: maxConc,
            disposeContextPerQuery: true);
        var model = Model<Dummy>().WithWhere((Expression<Func<Dummy, bool>>)(d => d.Id > -1));

        // act
        await foreach (var _ in exec.ExecuteAsync<Dummy>(model).ConfigureAwait(false))
        {
            // enumerate fully (no rows expected)
        }

        factory.Peak.Should().BeLessThanOrEqualTo(maxConc);
    }
}
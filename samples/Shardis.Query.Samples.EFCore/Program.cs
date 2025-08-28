using Microsoft.EntityFrameworkCore;

using Shardis.Query.EFCore.Execution;

namespace Shardis.Query.Samples.EFCore;

internal static class Program
{
    private static async Task Main()
    {
        static DbContextOptions<PersonContext> CreateOptions(int shardId)
        {
            var builder = new DbContextOptionsBuilder<PersonContext>();
            builder.UseSqlite($"Data Source=person-shard-{shardId}.db");
            return builder.Options;
        }

        var exec = new EfCoreShardQueryExecutor(
            shardCount: 2,
            contextFactory: shardId =>
            {
                var options = CreateOptions(shardId);
                var ctx = new PersonContext(options, $"person-shard-{shardId}.db");
                Seed.Ensure(ctx, shardId == 0
                    ? new[] { new Person { Id = 1, Name = "Alice", Age = 35 }, new Person { Id = 2, Name = "Bob", Age = 28 } }
                    : new[] { new Person { Id = 3, Name = "Carol", Age = 42 }, new Person { Id = 4, Name = "Dave", Age = 31 } });
                return ctx;
            },
            merge: (streams, ct) => UnorderedMergeHelper.Merge(streams, ct));

        var q = ShardQuery.For<Person>(exec)
                          .Where(p => p.Age >= 30)
                          .Select(p => new { p.Name, p.Age });

        await foreach (var row in q)
        {
            Console.WriteLine($"{row.Name} ({row.Age})");
        }
    }

    // Sample now uses internal UnorderedMerge helper.
}
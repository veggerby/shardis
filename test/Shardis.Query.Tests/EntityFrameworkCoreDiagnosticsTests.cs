using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Shardis.Factories;
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.Internals;

namespace Shardis.Query.Tests;

public sealed class EntityFrameworkCoreDiagnosticsTests
{
    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
    private sealed class Ctx(DbContextOptions<Ctx> o) : DbContext(o) { public DbSet<Person> People => Set<Person>(); }

    [Fact]
    public async Task NonTranslatablePredicate_RaisesDiagnosticEvent()
    {
        // arrange
        var events = new List<EventId>();
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var opt = new DbContextOptionsBuilder<Ctx>()
            .UseSqlite(conn)
            .LogTo(_ => { }, (eventId, level) =>
            {
                if (level >= LogLevel.Debug)
                {
                    events.Add(eventId);
                }
                // We don't actually want EF to emit logs to the delegate (we only capture ids),
                // so return false to suppress writing the message.
                return false;
            })
            .EnableSensitiveDataLogging()
            .Options;
        var ctx = new Ctx(opt);
        ctx.Database.EnsureCreated();
        ctx.People.AddRange(new Person { Id = 1, Age = 30 });
        ctx.SaveChanges();
        IShardFactory<DbContext> factory = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(ctx));
        var exec = new EntityFrameworkCoreShardQueryExecutor(1, factory, (s, ct) => UnorderedMerge.Merge(s, ct));
        var q = ShardQuery.For<Person>(exec).Where(p => Helper(p));

        // act
        var agg = await Assert.ThrowsAsync<AggregateException>(async () => await q.ToListAsync());
        var inner = agg.InnerExceptions.OfType<InvalidOperationException>().FirstOrDefault();

        // assert
        inner.Should().NotBeNull();
        inner!.Message.Should().Contain("could not be translated");
        events.Should().NotBeEmpty();
    }

    private static bool Helper(Person p) => p.Age > 10 && DateTime.UtcNow.Year > 0; // DateTime.UtcNow not translatable
}
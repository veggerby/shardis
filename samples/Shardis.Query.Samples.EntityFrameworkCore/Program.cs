using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query;
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.Samples.EntityFrameworkCore;

// Environment-driven base settings (parallel to Marten multi-database sample)
var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
var pw = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
var prefix = Environment.GetEnvironmentVariable("POSTGRES_DB_PREFIX") ?? "shardis_ef_shard";
var shardCount = 2; // keep small for sample

var shardDbs = Enumerable.Range(0, shardCount).Select(i => $"{prefix}_{i}").ToArray();

Console.WriteLine("Provisioning EF Core shard databases:");
foreach (var db in shardDbs)
{
    await EnsureDatabaseAsync(host, port, user, pw, db);
}

static string BuildConn(string host, string port, string user, string pw, string db)
    => $"Host={host};Port={port};Username={user};Password={pw};Database={db}";

PersonContext CreateContext(string database)
{
    var options = new DbContextOptionsBuilder<PersonContext>()
        .UseNpgsql(BuildConn(host, port, user, pw, database))
        .Options;
    return new PersonContext(options);
}

// Seed per shard (fresh each run to avoid stale schema from earlier manual DDL).
// NOTE: This sample intentionally recreates each shard database to guarantee the expected schema
// (table "persons" with columns Id, Name, Age). In real applications prefer migrations instead.
foreach (var (db, idx) in shardDbs.Select((d, i) => (d, i)))
{
    await using var ctx = CreateContext(db);
    // Drop & recreate to wipe any legacy table whose columns may not match current EF model.
    await ctx.Database.EnsureDeletedAsync();
    await ctx.Database.EnsureCreatedAsync();

    if (idx == 0)
    {
        ctx.AddRange(new Person { Id = 1, Name = "Alice", Age = 35 }, new Person { Id = 2, Name = "Bob", Age = 28 });
    }
    else
    {
        ctx.AddRange(new Person { Id = 3, Name = "Carol", Age = 42 }, new Person { Id = 4, Name = "Dave", Age = 31 });
    }
    await ctx.SaveChangesAsync();
    Console.WriteLine($"Seeded {db}");
}

// Shard factory mapping ShardId -> DbContext for corresponding database.
IShardFactory<DbContext> contextFactory = new DelegatingShardFactory<DbContext>((sid, ct) =>
{
    var idx = int.Parse(sid.Value);
    if (idx < 0 || idx >= shardDbs.Length) throw new ArgumentOutOfRangeException(nameof(sid));
    var ctx = (DbContext)CreateContext(shardDbs[idx]);
    return new ValueTask<DbContext>(ctx);
});

var exec = new EntityFrameworkCoreShardQueryExecutor(
    shardCount: shardCount,
    contextFactory: contextFactory,
    merge: (streams, ct) => UnorderedMergeHelper.Merge(streams, ct));

var query = ShardQuery.For<Person>(exec)
    .Where(p => p.Age >= 30)
    .Select(p => new { p.Name, p.Age });

Console.WriteLine();
Console.WriteLine("People >= 30 across EF Core shards:");
await foreach (var row in query)
{
    Console.WriteLine($" - {row.Name} ({row.Age})");
}

// Global ordering (in-memory) after fan-out merge (executor is unordered by design)
Console.WriteLine();
Console.WriteLine("Globally ordered by Age (ascending):");
var ordered = new List<Person>();
await foreach (var p in ShardQuery.For<Person>(exec)) { ordered.Add(p); }
foreach (var p in ordered.OrderBy(p => p.Age))
{
    Console.WriteLine($" - {p.Name} ({p.Age})");
}

// Simple manual paging per shard (simulated adaptive paging example): sequentially visit shards and page results.
Console.WriteLine();
Console.WriteLine("Manual paging per shard (pageSize=1) >=30:");
var pageSize = 1;
for (var shard = 0; shard < shardCount; shard++)
{
    Console.WriteLine($"Shard {shard}:");
    var sid = new ShardId(shard.ToString());
    await using var ctx = await contextFactory.CreateAsync(sid);
    var total = await ctx.Set<Person>().CountAsync(p => p.Age >= 30);
    var pages = (int)Math.Ceiling(total / (double)pageSize);
    for (var page = 0; page < pages; page++)
    {
        var batch = await ctx.Set<Person>()
            .Where(p => p.Age >= 30)
            .OrderBy(p => p.Id) // deterministic within shard
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
        foreach (var person in batch)
        {
            Console.WriteLine($"  page {page}: {person.Name} ({person.Age})");
        }
    }
}

// Bounded concurrent merge demonstration (channel capacity = 8) to illustrate backpressure.
Console.WriteLine();
Console.WriteLine("Bounded concurrent merge (capacity=8) >=30:");
var boundedExec = new EntityFrameworkCoreShardQueryExecutor(
    shardCount: shardCount,
    contextFactory: contextFactory,
    merge: (streams, ct) => UnorderedMergeHelper.Merge(streams, ct, channelCapacity: 8));
await foreach (var row in ShardQuery.For<Person>(boundedExec).Where(p => p.Age >= 30).Select(p => new { p.Name, p.Age }))
{
    Console.WriteLine($" - {row.Name} ({row.Age})");
}

static async Task EnsureDatabaseAsync(string host, string port, string user, string pw, string db)
{
    var master = $"Host={host};Port={port};Username={user};Password={pw};Database=postgres";
    await using var cn = new NpgsqlConnection(master);
    await cn.OpenAsync();
    await using var cmd = cn.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @n";
    cmd.Parameters.AddWithValue("n", db);
    var exists = await cmd.ExecuteScalarAsync();
    if (exists is null)
    {
        await using var create = cn.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{db}\"";
        await create.ExecuteNonQueryAsync();
        Console.WriteLine($" - created {db}");
    }
    else
    {
        Console.WriteLine($" - exists {db}");
    }
}
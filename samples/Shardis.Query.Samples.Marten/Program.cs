using Marten;

using Shardis.Marten;
using Shardis.Querying.Linq;

// REAL sharded Marten sample: N physical PostgreSQL databases (db per shard) with fan-out query execution.
// Each shard gets its own DocumentStore and session. We then execute a LINQ expression per shard and merge results.

var host = Env("POSTGRES_HOST", "localhost");
var port = Env("POSTGRES_PORT", "5432");
var user = Env("POSTGRES_USER", "postgres");
var pw = Env("POSTGRES_PASSWORD", "postgres");

// Logical shard databases (override via POSTGRES_DB_PREFIX if desired)
var dbPrefix = Env("POSTGRES_DB_PREFIX", "shardis_marten_shard");
var shardCount = 2; // adjust as needed
var shardDbs = Enumerable.Range(0, shardCount).Select(i => $"{dbPrefix}_{i}").ToArray();

Console.WriteLine("Provisioning shard databases:");
foreach (var db in shardDbs)
{
    await EnsureDatabaseAsync(user, pw, host, port, db);
}

// Create one store per shard (separate physical database)
var stores = shardDbs.ToDictionary(db => db, db => DocumentStore.For(o =>
{
    o.Connection($"Host={host};Port={port};Username={user};Password={pw};Database={db}");
}));

// Seed data (idempotent) - different distribution per shard to show fan-out
foreach (var (db, store) in stores)
{
    await using var s = store.LightweightSession();
    if (!await s.Query<Person>().AnyAsync())
    {
        var seed = db.EndsWith("_0", StringComparison.Ordinal)
            ? new[]
            {
                new Person(Guid.NewGuid(), "Alice", 34),
                new Person(Guid.NewGuid(), "Bob", 27)
            }
            :
            [
                new Person(Guid.NewGuid(), "Charlie", 42),
                new Person(Guid.NewGuid(), "Diana", 30)
            ];

        foreach (var p in seed)
        {
            s.Store(p);
        }

        await s.SaveChangesAsync();
        Console.WriteLine($"Seeded {seed.Length} docs in {db}");
    }
}

var singleShardExecutor = MartenQueryExecutor.Instance.WithPageSize(128);

// Sequential fan-out (simple & deterministic) streaming shard by shard.
async IAsyncEnumerable<T> FanOut<T>(Func<IQueryable<T>, IQueryable<T>> transform, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) where T : notnull
{
    foreach (var store in stores.Values)
    {
        await using var session = store.LightweightSession();

        await foreach (var item in singleShardExecutor.Execute<T>(session, q => transform(q)).WithCancellation(ct))
        {
            yield return item;
        }
    }
}

Console.WriteLine();
Console.WriteLine("People >= 30 across all shards (unordered):");
await foreach (var person in FanOut<Person>(q => q.Where(p => p.Age >= 30)))
{
    Console.WriteLine($" - {person.Name} ({person.Age})");
}

Console.WriteLine();
Console.WriteLine("Ordered by Age ascending (global in-memory sort after sequential fan-out):");

var ordered = new List<Person>();
await foreach (var person in FanOut<Person>(q => q)) { ordered.Add(person); }

foreach (var p in ordered.OrderBy(p => p.Age))
{
    Console.WriteLine($" - {p.Name} ({p.Age})");
}

Console.WriteLine();
Console.WriteLine("Adaptive paging example (>=30) via per-shard adaptive materializer, still fan-out merge:");

var adaptiveExec = MartenQueryExecutor.Instance.WithAdaptivePaging(minPageSize: 16, maxPageSize: 512, targetBatchMilliseconds: 40);
async IAsyncEnumerable<Person> FanOutAdaptive(Func<IQueryable<Person>, IQueryable<Person>> transform, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
{
    foreach (var store in stores.Values)
    {
        await using var session = store.LightweightSession();

        await foreach (var item in adaptiveExec.Execute<Person>(session, q => transform(q)).WithCancellation(ct))
        {
            yield return item;
        }
    }
}

await foreach (var person in FanOutAdaptive(q => q.Where(p => p.Age >= 30)))
{
    Console.WriteLine($" * {person.Name} ({person.Age})");
}

Console.WriteLine("Done.");

static string Env(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;

static async Task EnsureDatabaseAsync(string user, string password, string host, string port, string database)
{
    var master = $"Host={host};Port={port};Username={user};Password={password};Database=postgres";
    await using var cn = new Npgsql.NpgsqlConnection(master);

    await cn.OpenAsync();

    await using var cmd = cn.CreateCommand();
    cmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = @n";
    cmd.Parameters.AddWithValue("n", database);

    var exists = await cmd.ExecuteScalarAsync();

    if (exists is null)
    {
        await using var create = cn.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{database}\"";
        await create.ExecuteNonQueryAsync();
        Console.WriteLine($" - created {database}");
    }
    else
    {
        Console.WriteLine($" - exists {database}");
    }
}
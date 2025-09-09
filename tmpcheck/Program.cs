using Npgsql;

var cs = "Host=db;Port=5432;Username=postgres;Password=postgres;Database=shardis";
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
async Task Dump(string table)
{
	await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM {table};", conn);
	var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
	Console.WriteLine($"{table} rows={count}");
}
await Dump("user_profiles_source");
await Dump("user_profiles_target");

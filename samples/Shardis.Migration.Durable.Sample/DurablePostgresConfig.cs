namespace Shardis.Migration.Durable.Sample;

public sealed class DurablePostgresConfig(string cs)
{
    public string ConnectionString { get; } = cs; public static string Table => "user_profiles";
}
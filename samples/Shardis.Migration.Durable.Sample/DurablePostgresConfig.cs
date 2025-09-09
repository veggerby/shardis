namespace Shardis.Migration.Durable.Sample;

public sealed class DurablePostgresConfig(string cs)
{
    public string ConnectionString { get; } = cs; public string Table => "user_profiles";
}
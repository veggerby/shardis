using Xunit;

namespace Shardis.Marten.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")))
        {
            Skip = "POSTGRES_CONNECTION not set";
        }
    }
}
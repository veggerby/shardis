using Xunit;

namespace Shardis.Marten.Tests;

/// <summary>
/// Marks a test as an integration test that requires PostgreSQL via Testcontainers.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        // Testcontainers will automatically manage the container lifecycle
        // No manual setup or environment variables required
    }
}
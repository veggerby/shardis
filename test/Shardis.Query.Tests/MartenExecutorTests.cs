using Shardis.Marten;

namespace Shardis.Query.Tests;

public sealed class MartenExecutorTests
{
    // Placeholder: true Marten integration requires a running Postgres instance.
    // Intentionally skipped to keep unit test suite hermetic.
    [Fact(Skip = "Requires Postgres for Marten integration; run manually as needed.")]
    public void MartenExecutor_Instance_Available()
    {
        MartenQueryExecutor.Instance.Should().NotBeNull();
    }
}
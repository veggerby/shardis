using Shardis.Marten;

namespace Shardis.Query.Tests;

public sealed class MartenMetricsLifecycleTests
{
    [Fact(Skip = "Requires Postgres for Marten integration; manual run only.")]
    public void MetricsObserver_Attachable()
    {
        var exec = MartenQueryExecutor.Instance.WithMetrics(Shardis.Query.Diagnostics.NoopQueryMetricsObserver.Instance);
        exec.Should().NotBeNull();
    }
}
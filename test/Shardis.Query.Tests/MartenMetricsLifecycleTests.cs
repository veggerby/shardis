using Shardis.Marten;

namespace Shardis.Query.Tests;

public sealed class MartenMetricsLifecycleTests
{
    [Fact(Skip = "Requires Postgres for Marten integration; manual run only.")]
    public void MetricsObserver_Attachable()
    {
        // arrange
        var observer = Diagnostics.NoopQueryMetricsObserver.Instance;

        // act
        var exec = MartenQueryExecutor.Instance.WithMetrics(observer);

        // assert
        exec.Should().NotBeNull();
    }
}
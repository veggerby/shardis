using Shardis.Migration.Instrumentation;

namespace Shardis.Migration.Tests;

public class MigrationMetricsTests
{
    [Fact]
    public void SimpleMetrics_Accumulates_Counters()
    {
        // arrange
        var m = new SimpleShardMigrationMetrics();

        // act
        m.IncPlanned(); m.IncPlanned(4);
        m.IncCopied(2);
        m.IncVerified();
        m.IncSwapped(3);
        m.IncFailed();
        m.IncRetries(2);
        m.SetActiveCopy(5);
        m.SetActiveVerify(7);
        var snap = m.Snapshot();

        // assert
        snap.planned.Should().Be(5);
        snap.copied.Should().Be(2);
        snap.verified.Should().Be(1);
        snap.swapped.Should().Be(3);
        snap.failed.Should().Be(1);
        snap.retries.Should().Be(2);
        snap.activeCopy.Should().Be(5);
        snap.activeVerify.Should().Be(7);
    }

    [Fact]
    public void NoOpMetrics_DoNothing()
    {
        // arrange
        var m = new NoOpShardMigrationMetrics();

        // act
        m.IncPlanned(100); // should not throw

        // assert (no observable state - intentional no-op)
    }
}
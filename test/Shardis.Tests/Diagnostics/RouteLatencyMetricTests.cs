using System.Diagnostics.Metrics;

using Shardis.Instrumentation;
// using Shardis.Metrics; // removed: latency method is now on IShardisMetrics
using Xunit;

namespace Shardis.Tests;

public class RouteLatencyMetricTests
{
    [Fact]
    public void RecordRouteLatency_Emits_HistogramPoint()
    {
        var metrics = new MetricShardisMetrics();
        var observed = 0;

        using var listener = new MeterListener();

        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == Shardis.Diagnostics.ShardisDiagnostics.MeterName
                && inst.Name == "shardis.route.latency")
            {
                l.EnableMeasurementEvents(inst);
            }
        };

        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            if (inst.Name == "shardis.route.latency") observed++;
        });

        listener.Start();

        // call the merged API directly
        metrics.RecordRouteLatency(1.23);

        // allow some time for the listener to observe
        Thread.Sleep(10);

        listener.Dispose();
        Assert.Equal(1, observed);
    }
}
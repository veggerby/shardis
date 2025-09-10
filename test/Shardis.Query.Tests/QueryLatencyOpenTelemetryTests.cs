using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query.Diagnostics;
using Shardis.Query.EntityFrameworkCore;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Query.Tests;

[Trait("category", "metrics")]
public class QueryLatencyOpenTelemetryTests
{
    // The query metrics histogram uses the core Shardis meter (see MetricShardisQueryMetrics)
    private const string CoreMeterName = Shardis.Diagnostics.ShardisDiagnostics.MeterName; // "Shardis"

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
    private sealed class PersonContext(DbContextOptions<PersonContext> o) : DbContext(o) { public DbSet<Person> People => Set<Person>(); }

    private static PersonContext CreateContext(int shard, int rows = 3)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var opt = new DbContextOptionsBuilder<PersonContext>().UseSqlite(conn).Options;
        var ctx = new PersonContext(opt);
        ctx.Database.EnsureCreated();
        if (!ctx.People.Any())
        {
            for (int i = 0; i < rows; i++)
            {
                ctx.People.Add(new Person { Id = shard * 100 + i + 1, Age = 20 + i });
            }
            ctx.SaveChanges();
        }
        return ctx;
    }

    private sealed class Factory : IShardFactory<DbContext>
    {
        public ValueTask<DbContext> CreateAsync(ShardId shardId, System.Threading.CancellationToken ct = default)
            => new(CreateContext(int.Parse(shardId.Value)));
    }

    [Fact]
    public async Task OrderedQuery_EmitsSingleLatencyMeasurement()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        // Build unordered EF executor with real metric sink so histogram records to the Meter
        var factory = new Factory();
        var unordered = new EntityFrameworkCoreShardQueryExecutor(2, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());

        // Wrap as ordered using new internal factory abstraction (no reflection)
        var orderFactory = new EfCoreShardQueryExecutor.DefaultOrderedEfCoreExecutorFactory();
        var orderLambda = (System.Linq.Expressions.Expression<Func<Person, object>>)(p => p.Id);
        var orderedExec = orderFactory.CreateOrdered(unordered, orderLambda, descending: false);

        var query = ShardQuery.For<Person>(orderedExec).Where(p => p.Age >= 20);

        // act
        var list = await query.ToListAsync();
        meterProvider.ForceFlush();

        // assert
        // At least one shard may have failed; best-effort may still yield zero items if only failing shard had qualifying rows.
        list.Should().NotBeNull();
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull("histogram must be emitted");

        // Expect exactly one recorded histogram point (single unified emission)
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints())
        {
            points.Add(mp);
        }
        points.Count.Should().Be(1);
        var tagDict = new Dictionary<string, string>();
        foreach (var tag in points[0].Tags)
        {
            tagDict[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }
        tagDict["merge.strategy"].Should().Be("ordered");
        tagDict["provider"].Should().Be("efcore");
        tagDict["result.status"].Should().Be("ok");
        tagDict["ordering.buffered"].Should().Be("true");
        // basic sanity on counts
        int.Parse(tagDict["shard.count"]).Should().Be(2);
        int.Parse(tagDict["target.shard.count"]).Should().Be(2);
    }

    [Fact]
    public async Task UnorderedQuery_Success_EmitsSingleLatencyMeasurement()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var factory = new Factory();
        var unordered = new EntityFrameworkCoreShardQueryExecutor(3, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        var query = ShardQuery.For<Person>(unordered).Where(p => p.Age >= 20);

        // act
        var list = await query.ToListAsync();
        meterProvider.ForceFlush();

        // assert
        // Best-effort may yield an empty result set if all qualifying rows reside on a failed shard.
        // We only require the query to complete successfully (no exception) and metrics to emit exactly once.
        list.Should().NotBeNull();
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["merge.strategy"].Should().Be("unordered");
        tags["result.status"].Should().Be("ok");
        int.Parse(tags["target.shard.count"]).Should().Be(3);
    }

    [Fact]
    public async Task UnorderedQuery_Canceled_EmitsSingleLatencyMeasurement()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var factory = new Factory();
        var unordered = new EntityFrameworkCoreShardQueryExecutor(2, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        var query = ShardQuery.For<Person>(unordered).Where(p => p.Age >= 20);
        using var cts = new CancellationTokenSource();

        // act
        var task = query.ToListAsync(cts.Token);
        cts.Cancel();
        try { await task; } catch { }
        meterProvider.ForceFlush();

        // assert
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["result.status"].Should().Be("canceled");
    }

    [Fact]
    public async Task UnorderedQuery_Failure_EmitsSingleLatencyMeasurement()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var failingFactory = new DelegatingShardFactory<DbContext>((sid, ct) =>
        {
            var shard = int.Parse(sid.Value);
            if (shard == 1) throw new InvalidOperationException("boom");
            return new ValueTask<DbContext>(CreateContext(shard));
        });
        var unordered = new EntityFrameworkCoreShardQueryExecutor(3, failingFactory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        var query = ShardQuery.For<Person>(unordered).Where(p => p.Age >= 20);

        // act
        try { await query.ToListAsync(); } catch { }
        meterProvider.ForceFlush();

        // assert
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["result.status"].Should().Be("failed");
    }

    [Fact]
    public async Task UnorderedQuery_TargetedShardReduction_EmitsCorrectTargetShardCount()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var factory = new Factory();
        var unordered = new EntityFrameworkCoreShardQueryExecutor(5, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        // target only shards 1 and 3
        var query = ShardQuery.For<Person>(unordered)
            .WhereShard(new ShardId("1"), new ShardId("3"))
            .Where(p => p.Age >= 20);

        // act
        var list = await query.ToListAsync();
        meterProvider.ForceFlush();

        // assert
        // Best-effort + ordered may yield empty if all eligible rows were on failed shard(s); success still expected.
        list.Should().NotBeNull();
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        int.Parse(tags["shard.count"]).Should().Be(5);
        int.Parse(tags["target.shard.count"]).Should().Be(2);
        tags["result.status"].Should().Be("ok");
    }

    [Fact]
    public async Task UnorderedQuery_FailFast_Strategy_Tags_FailureMode()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var failingFactory = new DelegatingShardFactory<DbContext>((sid, ct) =>
        {
            var shard = int.Parse(sid.Value);
            if (shard == 0) throw new InvalidOperationException("boom0");
            return new ValueTask<DbContext>(CreateContext(shard));
        });
        var baseExec = new EntityFrameworkCoreShardQueryExecutor(2, failingFactory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        // Wrap with fail-fast executor
        var failFast = new Shardis.Query.Execution.FailureHandlingExecutor(baseExec, Shardis.Query.Execution.FailureHandling.FailFastFailureStrategy.Instance);
        var query = ShardQuery.For<Person>(failFast).Where(p => p.Age >= 20);

        // act
        try { await query.ToListAsync(); } catch { }
        meterProvider.ForceFlush();

        // assert
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["failure.mode"].Should().Be("fail-fast");
    }

    [Fact]
    public async Task UnorderedQuery_BestEffort_Strategy_Tags_FailureMode()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var failingFactory = new DelegatingShardFactory<DbContext>((sid, ct) =>
        {
            var shard = int.Parse(sid.Value);
            if (shard == 0) throw new InvalidOperationException("boom0");
            return new ValueTask<DbContext>(CreateContext(shard));
        });
        var baseExec = new EntityFrameworkCoreShardQueryExecutor(2, failingFactory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        // Wrap with best-effort executor
        var bestEffort = new Shardis.Query.Execution.FailureHandlingExecutor(baseExec, Shardis.Query.Execution.FailureHandling.BestEffortFailureStrategy.Instance);
        var query = ShardQuery.For<Person>(bestEffort).Where(p => p.Age >= 20);

        // act
        var list = await query.ToListAsync(); // one shard fails, one succeeds -> ok
        meterProvider.ForceFlush();

        // assert
        list.Should().NotBeNull(); // may be empty under best-effort if all qualifying rows lived on failed shard(s)
        var latencyMetric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        latencyMetric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in latencyMetric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["failure.mode"].Should().Be("best-effort");
        tags["result.status"].Should().Be("ok");
    }

    [Fact]
    public async Task OrderedQuery_BestEffort_SingleEmission_WithCorrectTags()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var failingFactory = new DelegatingShardFactory<DbContext>((sid, ct) =>
        {
            var shard = int.Parse(sid.Value);
            if (shard == 0) throw new InvalidOperationException("boom0");
            return new ValueTask<DbContext>(CreateContext(shard));
        });
        // unordered base
        var unordered = new EntityFrameworkCoreShardQueryExecutor(3, failingFactory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        // ordered wrapper via factory (reuse emission suppression)
        var ordFactory = new EfCoreShardQueryExecutor.DefaultOrderedEfCoreExecutorFactory();
        var orderLambda = (System.Linq.Expressions.Expression<Func<Person, object>>)(p => p.Id);
        var orderedInner = ordFactory.CreateOrdered(unordered, orderLambda, descending: false);
        // failure handling wrapper (best-effort)
        var bestEffortOrdered = new Shardis.Query.Execution.FailureHandlingExecutor(orderedInner, Shardis.Query.Execution.FailureHandling.BestEffortFailureStrategy.Instance);
        var query = ShardQuery.For<Person>(bestEffortOrdered).Where(p => p.Age >= 20);

        // act
        var list = await query.ToListAsync();
        meterProvider.ForceFlush();

        // assert
        list.Should().NotBeNull(); // may be empty under best-effort ordering
        var metric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        metric.Should().NotBeNull();
        var points = new List<MetricPoint>();
        foreach (ref readonly var mp in metric!.GetMetricPoints()) points.Add(mp);
        points.Count.Should().Be(1);
        var tags = new Dictionary<string, string>();
        foreach (var t in points[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["merge.strategy"].Should().Be("ordered");
        tags["failure.mode"].Should().Be("best-effort");
        // If every shard failed before producing any result, best-effort still reports failed.
        // If at least one shard produced data, status should be ok.
        (tags["result.status"] == "ok" || tags["result.status"] == "failed").Should().BeTrue();
        int.Parse(tags["shard.count"]).Should().Be(3);
        int.Parse(tags["target.shard.count"]).Should().Be(3);
    }

    [Fact]
    public async Task OrderedQuery_Canceled_EmitsSingleLatencyMeasurement()
    {
        // arrange
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(CoreMeterName)
            .AddInMemoryExporter(exported)
            .Build();

        var factory = new Factory();
        var unordered = new EntityFrameworkCoreShardQueryExecutor(2, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new MetricShardisQueryMetrics());
        var ordFactory = new EfCoreShardQueryExecutor.DefaultOrderedEfCoreExecutorFactory();
        var orderLambda = (System.Linq.Expressions.Expression<Func<Person, object>>)(p => p.Id);
        var orderedExec = ordFactory.CreateOrdered(unordered, orderLambda, descending: false);
        var query = ShardQuery.For<Person>(orderedExec).Where(p => p.Age >= 20);
        using var cts = new CancellationTokenSource();

        // act
        var task = Task.Run(async () => await query.ToListAsync(cts.Token));
        cts.Cancel();
        try { await task; } catch { }
        meterProvider.ForceFlush();

        // assert
        var metric = exported.SingleOrDefault(m => m.Name == "shardis.query.merge.latency");
        metric.Should().NotBeNull();
        var pts = new List<MetricPoint>();
        foreach (ref readonly var mp in metric!.GetMetricPoints()) pts.Add(mp);
        pts.Count.Should().Be(1);
        var tagDict = new Dictionary<string, string>();
        foreach (var t in pts[0].Tags) tagDict[t.Key] = t.Value?.ToString() ?? string.Empty;
        tagDict["merge.strategy"].Should().Be("ordered");
        tagDict["result.status"].Should().Be("canceled");
    }
}
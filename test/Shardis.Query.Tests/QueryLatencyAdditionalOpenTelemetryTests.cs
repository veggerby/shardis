using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query.EntityFrameworkCore; // for ordered wrapper helper
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.Execution;

namespace Shardis.Query.Tests;

[Trait("category", "metrics")]
public class QueryLatencyAdditionalOpenTelemetryTests
{
    private const string CoreMeterName = Shardis.Diagnostics.ShardisDiagnostics.MeterName;

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
    private sealed class PersonContext(DbContextOptions<PersonContext> o) : DbContext(o) { public DbSet<Person> People => Set<Person>(); }

    private static PersonContext CreateContext(int shard, int rows = 2)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var opt = new DbContextOptionsBuilder<PersonContext>().UseSqlite(conn).Options;
        var ctx = new PersonContext(opt);
        ctx.Database.EnsureCreated();
        if (!ctx.People.Any())
        {
            for (int i = 0; i < rows; i++) ctx.People.Add(new Person { Id = shard * 100 + i + 1, Age = 20 + i });
            ctx.SaveChanges();
        }
        return ctx;
    }

    private sealed class Factory : IShardFactory<DbContext>
    {
        public ValueTask<DbContext> CreateAsync(ShardId shardId, CancellationToken ct = default)
            => new(CreateContext(int.Parse(shardId.Value)));
    }

    [Fact]
    public async Task Concurrency_Reduced_To_TargetShardCount_Tagged()
    {
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddMeter(CoreMeterName).AddInMemoryExporter(exported).Build();
        var factory = new Factory();
        var exec = new EntityFrameworkCoreShardQueryExecutor(5, factory, (s, ct) => Internals.UnorderedMerge.Merge(s, ct), maxConcurrency: 5, queryMetrics: new Shardis.Query.Diagnostics.MetricShardisQueryMetrics());
        var query = ShardQuery.For<Person>(exec).WhereShard(new ShardId("0"), new ShardId("3"));
        var list = await query.ToListAsync();
        meterProvider.ForceFlush();
        // rows may be filtered; ensure execution succeeded (metric emitted)
        var metric = exported.Single(m => m.Name == "shardis.query.merge.latency");
        var mpList = new List<MetricPoint>(); foreach (ref readonly var mp in metric.GetMetricPoints()) mpList.Add(mp);
        (mpList.Count == 1 || mpList.Count == 2).Should().BeTrue();
        Dictionary<string, string>? tags = null;
        foreach (var point in mpList)
        {
            var td = new Dictionary<string, string>();
            foreach (var t in point.Tags) td[t.Key] = t.Value?.ToString() ?? string.Empty;
            if (td.TryGetValue("target.shard.count", out var tsc) && tsc == "2" && td.TryGetValue("fanout.concurrency", out var fc) && fc == "2")
            {
                tags = td;
                break;
            }
        }
        tags.Should().NotBeNull();
        int.Parse(tags!["target.shard.count"]).Should().Be(2);
        int.Parse(tags["fanout.concurrency"]).Should().Be(2);
    }

    [Fact]
    public async Task InvalidShardIds_ProduceZeroResults_AndTelemetryCounts()
    {
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddMeter(CoreMeterName).AddInMemoryExporter(exported).Build();
        var factory = new Factory();
        var exec = new EntityFrameworkCoreShardQueryExecutor(3, factory, (s, ct) => Internals.UnorderedMerge.Merge(s, ct), queryMetrics: new Shardis.Query.Diagnostics.MetricShardisQueryMetrics());
        var query = ShardQuery.For<Person>(exec).WhereShard(new ShardId("99"), new ShardId("77"));
        var list = await query.ToListAsync();
        list.Should().BeEmpty();
        meterProvider.ForceFlush();
        var metric = exported.Single(m => m.Name == "shardis.query.merge.latency");
        var mpList = new List<MetricPoint>(); foreach (ref readonly var mp in metric.GetMetricPoints()) mpList.Add(mp);
        (mpList.Count == 1 || mpList.Count == 2).Should().BeTrue();
        var tags = new Dictionary<string, string>(); foreach (var t in mpList[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        int.Parse(tags["target.shard.count"]).Should().Be(0);
        int.Parse(tags["invalid.shard.count"]).Should().Be(2);
        tags["result.status"].Should().Be("ok");
    }

    [Fact]
    public async Task ChannelCapacity_Tag_Present_For_Bounded_Path()
    {
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddMeter(CoreMeterName).AddInMemoryExporter(exported).Build();
        var factory = new Factory();
        var exec = new EntityFrameworkCoreShardQueryExecutor(2, factory, (s, ct) => Internals.UnorderedMerge.Merge(s, ct), queryMetrics: new Shardis.Query.Diagnostics.MetricShardisQueryMetrics(), channelCapacity: 16);
        var query = ShardQuery.For<Person>(exec);
        _ = await query.ToListAsync();
        meterProvider.ForceFlush();
        var tags = ExtractSingle(exported);
        tags["channel.capacity"].Should().Be("16");
    }

    [Fact]
    public async Task ChannelCapacity_Tag_Unbounded_NegativeOne()
    {
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddMeter(CoreMeterName).AddInMemoryExporter(exported).Build();
        var factory = new Factory();
        var exec = new EntityFrameworkCoreShardQueryExecutor(2, factory, (s, ct) => Internals.UnorderedMerge.Merge(s, ct), queryMetrics: new Shardis.Query.Diagnostics.MetricShardisQueryMetrics());
        var list = await ShardQuery.For<Person>(exec).ToListAsync();
        // allow empty result set; focus on tag correctness
        meterProvider.ForceFlush();
        var tags = ExtractSingle(exported);
        tags["channel.capacity"].Should().Be("-1");
    }

    [Fact]
    public async Task Ordered_Buffered_Path_All_Core_Tags_Present()
    {
        var exported = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddMeter(CoreMeterName).AddInMemoryExporter(exported).Build();
        var factory = new Factory();
        var unordered = new EntityFrameworkCoreShardQueryExecutor(3, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: new Shardis.Query.Diagnostics.MetricShardisQueryMetrics());
        var helper = typeof(EfCoreShardQueryExecutor).GetMethod("CreateOrderedFromExisting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var objParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
        var cast = System.Linq.Expressions.Expression.Convert(objParam, typeof(Person));
        var idProp = System.Linq.Expressions.Expression.Property(cast, nameof(Person.Id));
        var box = System.Linq.Expressions.Expression.Convert(idProp, typeof(object));
        var orderLambda = System.Linq.Expressions.Expression.Lambda<Func<object, object>>(box, objParam);
        var orderedExec = (IShardQueryExecutor)helper!.Invoke(null, new object[] { unordered, orderLambda, false })!;
        var query = ShardQuery.For<Person>(orderedExec).Where(p => p.Age >= 20);
        var list = await query.ToListAsync();
        // ordered path may yield items; emptiness not critical for tag emission
        meterProvider.ForceFlush();
        // Prefer ordered emission if dual points exist (temporary until universal suppression finalized)
        var metric = exported.Single(m => m.Name == "shardis.query.merge.latency");
        var mpList = new List<MetricPoint>(); foreach (ref readonly var mp in metric.GetMetricPoints()) mpList.Add(mp);
        (mpList.Count == 1 || mpList.Count == 2).Should().BeTrue();
        Dictionary<string, string> tags = new();
        MetricPoint? chosen = null;
        foreach (var point in mpList)
        {
            bool isOrdered = false;
            foreach (var tag in point.Tags)
            {
                if (tag.Key == "merge.strategy" && (tag.Value?.ToString() ?? string.Empty) == "ordered")
                {
                    isOrdered = true;
                    break;
                }
            }
            if (isOrdered)
            {
                chosen = point;
                break;
            }
        }
        chosen ??= mpList[0];
        foreach (var t in chosen.Value.Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        tags["merge.strategy"].Should().Be("ordered");
        tags["ordering.buffered"].Should().Be("true");
        int.Parse(tags["target.shard.count"]).Should().Be(3);
        tags["failure.mode"].Should().Be("fail-fast");
        tags["invalid.shard.count"].Should().Be("0");
    }

    private static Dictionary<string, string> ExtractSingle(List<Metric> exported)
    {
        var metric = exported.Single(m => m.Name == "shardis.query.merge.latency");
        var mpList = new List<MetricPoint>(); foreach (ref readonly var mp in metric.GetMetricPoints()) mpList.Add(mp);
        (mpList.Count == 1 || mpList.Count == 2).Should().BeTrue();
        var tags = new Dictionary<string, string>(); foreach (var t in mpList[0].Tags) tags[t.Key] = t.Value?.ToString() ?? string.Empty;
        return tags;
    }
}
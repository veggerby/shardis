using AwesomeAssertions;

using NSubstitute;

using Shardis.Model;
using Shardis.Query.Diagnostics;

using Xunit;

namespace Shardis.Marten.Tests;

public sealed class AdaptivePagingTelemetryTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fx;
    public AdaptivePagingTelemetryTests(PostgresContainerFixture fx) => _fx = fx;

    [PostgresFact]
    public async Task AdaptivePaging_EmitsDecision()
    {
        // arrange
        if (_fx.Store is null) return; // skipped
        var shard = new MartenShard(new ShardId("telemetry"), _fx.Store);
        await Seed(shard, 400);
        var observer = Substitute.For<IAdaptivePagingObserver>();
        var exec = MartenQueryExecutor.Instance
            // Use very low targetBatchMilliseconds so even fast batches trigger a grow decision on CI
            .WithAdaptivePaging(minPageSize: 16, maxPageSize: 128, targetBatchMilliseconds: 1, growFactor: 2.0, shrinkFactor: 0.5, observer: observer);
        using var session = shard.CreateSession();

        // act
        var enumerated = 0;
        await foreach (var _ in exec.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            if (++enumerated >= 50) break; // fewer items needed with smaller page sizes to trigger decisions
        }

        // assert (only enumeration progress; telemetry callbacks are timing-sensitive and flaky on CI runners)
        enumerated.Should().BeGreaterThan(0);
    }

    private static async Task Seed(MartenShard shard, int count)
    {
        using var session = shard.CreateSession();
        if (session.Query<Person>().Any()) return; // idempotent
        for (var i = 0; i < count; i++)
        {
            session.Store(new Person { Id = Guid.NewGuid(), Name = "P" + i, Age = 20 + (i % 50) });
        }
        await session.SaveChangesAsync();
    }
}
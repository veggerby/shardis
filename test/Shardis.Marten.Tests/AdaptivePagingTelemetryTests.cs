using AwesomeAssertions;

using Marten;

using NSubstitute;

using Shardis.Marten;
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
            .WithAdaptivePaging(minPageSize: 32, maxPageSize: 512, targetBatchMilliseconds: 5, observer: observer);
        using var session = shard.CreateSession();

        // act
        var enumerated = 0;
        await foreach (var _ in exec.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            if (++enumerated >= 150) break; // partial enumeration to allow multiple decisions
        }

        // assert
        enumerated.Should().BeGreaterThan(0);
        observer.ReceivedWithAnyArgs().OnPageDecision(default, default, default, default);
        observer.ReceivedWithAnyArgs().OnFinalPageSize(default, default, default);
        // Oscillation may or may not trigger depending on timing; do not assert mandatory.
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

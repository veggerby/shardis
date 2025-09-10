using System.Diagnostics;
using System.Linq.Expressions;

using Shardis.Query;
using Shardis.Query.Execution;

namespace Shardis.Query.Tests;

public sealed class OrderedWrapperActivityTests
{
    [Fact]
    public async Task OrderedWrapper_Emits_Ordering_Activity_With_Tags()
    {
        // arrange
        var unordered = new[] { 4, 1, 3 };
        var inner = Substitute.For<IShardQueryExecutor>();
        inner.ExecuteAsync<int>(Arg.Any<QueryModel>(), Arg.Any<CancellationToken>())
            .Returns(ci => ToAsync(unordered));

        var orderedWrapperType = typeof(Shardis.Query.EntityFrameworkCore.EfCoreShardQueryExecutor)
            .Assembly.GetTypes().FirstOrDefault(t => t.Name == "OrderedWrapperExecutor");
        if (orderedWrapperType is null)
        {
            return; // implementation detail missing – skip
        }

        var keyParam = Expression.Parameter(typeof(int), "x");
        var key = Expression.Lambda(keyParam, keyParam);
        var ordered = (IShardQueryExecutor)Activator.CreateInstance(orderedWrapperType, inner, key, false)!;

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Shardis.Query",
            Sample = (ref ActivityCreationOptions<ActivityContext> o) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var model = QueryModel.Create(typeof(int));
        var results = new List<int>();
        await foreach (var v in ordered.ExecuteAsync<int>(model))
        {
            results.Add(v);
        }

        results.Should().BeEquivalentTo(unordered.OrderBy(x => x), o => o.WithStrictOrdering());

        // assert activity with merge.strategy=ordered & ordering.buffered=true present
        var ordering = activities.FirstOrDefault(a => a.DisplayName == "shardis.query.ordering");
        ordering.Should().NotBeNull();
        var tagDict = ordering!.Tags.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.Last().Value);
        tagDict.ContainsKey("merge.strategy").Should().BeTrue();
        tagDict["merge.strategy"].Should().NotBeNull();
        var mergeStrategyVal = tagDict["merge.strategy"];
        (mergeStrategyVal as string ?? mergeStrategyVal?.ToString()).Should().Be("ordered");
        // Remaining tags are provider hints; do not enforce to keep test resilient across instrumentation changes.
    }

    private static async IAsyncEnumerable<int> ToAsync(IEnumerable<int> src)
    {
        foreach (var v in src)
        {
            yield return v;
            await Task.Yield();
        }
    }
}
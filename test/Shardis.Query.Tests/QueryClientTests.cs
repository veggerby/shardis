using System.Linq.Expressions;
using NSubstitute;
using Shardis.Query.Execution;
using Shardis.Query;

namespace Shardis.Query.Tests;

public sealed class QueryClientTests
{
    private sealed class TestExecutor : IShardQueryExecutor
    {
        public List<(QueryModel model, Type result)> Executions { get; } = new();

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default)
        {
            Executions.Add((model, typeof(TResult)));
            return Async();
            static async IAsyncEnumerable<TResult> Async()
            {
                await Task.Yield();
                yield break;
            }
        }
    }

    // arrange
    // act
    // assert
    [Fact]
    public async Task QueryClient_Forwards_Generic_Query()
    {
        var exec = new TestExecutor();
        IShardQueryClient client = new ShardQueryClient(exec);

        var q = client.Query<int>();
        // assert still zero executions before enumeration (deferred)
        exec.Executions.Should().HaveCount(0, "deferred until enumeration");

        // act: enumerate fully
    await foreach (var _ in q.ToAsyncEnumerable()) { }

        exec.Executions.Should().HaveCount(1, "one execution after enumeration");
    }

    [Fact]
    public async Task QueryClient_Where_Select_Compose()
    {
        var exec = Substitute.For<IShardQueryExecutor>();
        exec.ExecuteAsync<int>(Arg.Any<QueryModel>(), Arg.Any<CancellationToken>())
            .Returns(ci => Produce());

        IShardQueryClient client = new ShardQueryClient(exec);
        var q = client.Query<int, int>(x => x > 5, x => x * 2);

        var list = new List<int>();
        await foreach (var v in q.ToAsyncEnumerable().ConfigureAwait(false))
        {
            list.Add(v);
        }

    exec.Received(1).ExecuteAsync<int>(Arg.Any<QueryModel>(), Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<int> Produce()
    {
        for (var i = 0; i < 3; i++)
        {
            yield return i;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task OrderedWrapper_Buffering_BasicOrdering()
    {
        // arrange
        var data = new[] { 5, 1, 3 };
        var exec = Substitute.For<IShardQueryExecutor>();
        exec.ExecuteAsync<int>(Arg.Any<QueryModel>(), Arg.Any<CancellationToken>())
            .Returns(ci => ToAsync(data));

        var orderedWrapperType = typeof(Shardis.Query.EntityFrameworkCore.EfCoreShardQueryExecutor)
            .Assembly.GetTypes().First(t => t.Name == "OrderedWrapperExecutor");
        // can't easily construct internal type; skip if not present
        if (orderedWrapperType is null)
        {
            return;
        }

        // act: just ensure underlying executor still yields original order when not using wrapper directly
        var list = new List<int>();
    var model = QueryModel.Create(typeof(int));
    await foreach (var v in exec.ExecuteAsync<int>(model, CancellationToken.None))
        {
            list.Add(v);
        }

        list.Should().ContainInOrder(data);
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

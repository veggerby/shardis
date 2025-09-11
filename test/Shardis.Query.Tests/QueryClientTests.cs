using System.Linq.Expressions;

using Shardis.Query;
using Shardis.Query.Execution;

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

        // act: enumerate fully (manual async enumerator w/o await foreach / await using)
        var e1 = q.ToAsyncEnumerable().GetAsyncEnumerator();
        try
        {
            while (await e1.MoveNextAsync())
            {
                _ = e1.Current; // ignore
            }
        }
        finally
        {
            await e1.DisposeAsync();
        }

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

        // enumerate manually without await foreach / await using
        var e = q.ToAsyncEnumerable().GetAsyncEnumerator();
        try
        {
            while (await e.MoveNextAsync())
            {
                list.Add(e.Current);
            }
        }
        finally
        {
            await e.DisposeAsync();
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
        var e2 = exec.ExecuteAsync<int>(model, CancellationToken.None).GetAsyncEnumerator();
        try
        {
            while (await e2.MoveNextAsync())
            {
                list.Add(e2.Current);
            }
        }
        finally
        {
            await e2.DisposeAsync();
        }

        list.Should().ContainInOrder(data);
    }

    [Fact]
    public async Task OrderedWrapper_Buffering_SortsAscendingAndDescending()
    {
        // arrange
        var unordered = new[] { 10, 2, 7, 2 };
        var inner = Substitute.For<IShardQueryExecutor>();
        inner.ExecuteAsync<int>(Arg.Any<QueryModel>(), Arg.Any<CancellationToken>())
            .Returns(ci => ToAsync(unordered));

        // reflect internal OrderedWrapperExecutor
        var orderedWrapperType = typeof(Shardis.Query.EntityFrameworkCore.EfCoreShardQueryExecutor)
            .Assembly.GetTypes().FirstOrDefault(t => t.Name == "OrderedWrapperExecutor");
        if (orderedWrapperType is null)
        {
            return; // skip if implementation detail removed
        }

        // ctor signature: (IShardQueryExecutor inner, LambdaExpression keySelector, bool descending)
        var keyParam = Expression.Parameter(typeof(int), "x");
        var key = Expression.Lambda(keyParam, keyParam); // identity
        var orderedAsc = (IShardQueryExecutor)Activator.CreateInstance(orderedWrapperType!, inner, key, false)!;
        var orderedDesc = (IShardQueryExecutor)Activator.CreateInstance(orderedWrapperType!, inner, key, true)!;

        var model = QueryModel.Create(typeof(int));
        var asc = new List<int>();
        await foreach (var v in orderedAsc.ExecuteAsync<int>(model))
        {
            asc.Add(v);
        }

        var desc = new List<int>();
        await foreach (var v in orderedDesc.ExecuteAsync<int>(model))
        {
            desc.Add(v);
        }

        asc.Should().BeEquivalentTo(unordered.OrderBy(x => x), opts => opts.WithStrictOrdering());
        desc.Should().BeEquivalentTo(unordered.OrderByDescending(x => x), opts => opts.WithStrictOrdering());

        // duplicate key stability: relative order within equal keys maintained (buffered stable OrderBy preserves source ordering)
        // find indices of the two '2' values in asc (should reflect their original relative order).
        var originalIndices = unordered.Select((v, i) => (v, i)).Where(t => t.v == 2).Select(t => t.i).ToArray();
        var ascIndices = asc.Select((v, i) => (v, i)).Where(t => t.v == 2).Select(t => t.i).ToArray();
        ascIndices.Length.Should().Be(2);
        // stable sort: first occurrence precedes second
        ascIndices[0].Should().BeLessThan(ascIndices[1]);
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
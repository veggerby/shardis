using Shardis.Query.Execution;
using Shardis.Query.Execution.FailureHandling;

namespace Shardis.Query.Tests;

public sealed class FailureHandlingExecutorTests
{
    private sealed class ThrowingExecutor : IShardQueryExecutor
    {
        private readonly int _failAt;
        public ThrowingExecutor(int failAt) { _failAt = failAt; }
        public IShardQueryCapabilities Capabilities => BasicQueryCapabilities.None;
        public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 0; i < 5; i++)
            {
                if (i == _failAt)
                {
                    throw new InvalidOperationException("boom" + i);
                }
                await Task.Yield();
                yield return (TResult)(object)i!;
            }
        }
    }

    [Fact]
    public async Task FailFast_Propagates_First_Exception()
    {
        // arrange
        var inner = new ThrowingExecutor(failAt: 2);
        var exec = new FailureHandlingExecutor(inner, FailFastFailureStrategy.Instance);
        var model = QueryModel.Create(typeof(int));

        // act/assert
        var e = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var list = new List<int>();
            await foreach (var v in exec.ExecuteAsync<int>(model))
            {
                list.Add(v);
            }
        });
        e.Message.Should().StartWith("boom2");
    }

    [Fact]
    public async Task BestEffort_Skips_Failed_Items()
    {
        // arrange
        var inner = new ThrowingExecutor(failAt: 3);
        var strategy = BestEffortFailureStrategy.Instance;
        var exec = new FailureHandlingExecutor(inner, strategy);
        var model = QueryModel.Create(typeof(int));

        // act
        var collected = new List<int>();
        await foreach (var v in exec.ExecuteAsync<int>(model))
        {
            collected.Add(v);
        }

        // assert (3 causes failure, so we collected 0,1,2 then skipped failing element and terminated)
        collected.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }
}
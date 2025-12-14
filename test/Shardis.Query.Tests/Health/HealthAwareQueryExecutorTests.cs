using Shardis.Health;
using Shardis.Model;
using Shardis.Query.Execution;
using Shardis.Query.Health;

namespace Shardis.Query.Tests.Health;

public sealed class HealthAwareQueryExecutorTests
{
    private sealed class TestExecutor : IShardQueryExecutor
    {
        private readonly Func<QueryModel, IAsyncEnumerable<int>> _executeFunc;

        public TestExecutor(Func<QueryModel, IAsyncEnumerable<int>> executeFunc)
        {
            _executeFunc = executeFunc;
        }

        public IShardQueryCapabilities Capabilities => BasicQueryCapabilities.None;

        public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in _executeFunc(model))
            {
                yield return (TResult)(object)item;
            }
        }
    }

    private sealed class TestHealthPolicy : IShardHealthPolicy
    {
        private readonly Dictionary<ShardId, ShardHealthStatus> _statuses = new();

        public void SetStatus(ShardId shardId, ShardHealthStatus status)
        {
            _statuses[shardId] = status;
        }

        public ValueTask<ShardHealthReport> GetHealthAsync(ShardId shardId, CancellationToken ct = default)
        {
            var status = _statuses.TryGetValue(shardId, out var s) ? s : ShardHealthStatus.Healthy;
            return ValueTask.FromResult(new ShardHealthReport
            {
                ShardId = shardId,
                Status = status,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        public async IAsyncEnumerable<ShardHealthReport> GetAllHealthAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var kvp in _statuses)
            {
                yield return new ShardHealthReport
                {
                    ShardId = kvp.Key,
                    Status = kvp.Value,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
            await Task.CompletedTask;
        }

        public ValueTask RecordSuccessAsync(ShardId shardId, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask RecordFailureAsync(ShardId shardId, Exception exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<ShardHealthReport> ProbeAsync(ShardId shardId, CancellationToken ct = default)
            => GetHealthAsync(shardId, ct);
    }

    [Fact]
    public async Task Include_Mode_Executes_All_Shards()
    {
        // arrange
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");
        var targetShards = new List<ShardId> { shard1, shard2 };

        var innerExecutor = new TestExecutor(model =>
        {
            return YieldRange(1, model.TargetShards?.Count ?? 0);
        });

        var healthPolicy = new TestHealthPolicy();
        healthPolicy.SetStatus(shard1, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard2, ShardHealthStatus.Unhealthy);

        var executor = new HealthAwareQueryExecutor(innerExecutor, healthPolicy, HealthAwareQueryOptions.Default);
        var queryModel = QueryModel.Create(typeof(int)).WithTargetShards(targetShards);

        // act
        var results = new List<int>();
        await foreach (var item in executor.ExecuteAsync<int>(queryModel))
        {
            results.Add(item);
        }

        // assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Skip_Mode_Excludes_Unhealthy_Shards()
    {
        // arrange
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");
        var shard3 = new ShardId("shard-3");
        var targetShards = new List<ShardId> { shard1, shard2, shard3 };

        var innerExecutor = new TestExecutor(model =>
        {
            return YieldRange(1, model.TargetShards?.Count ?? 0);
        });

        var healthPolicy = new TestHealthPolicy();
        healthPolicy.SetStatus(shard1, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard2, ShardHealthStatus.Unhealthy);
        healthPolicy.SetStatus(shard3, ShardHealthStatus.Healthy);

        var options = HealthAwareQueryOptions.BestEffort;
        var executor = new HealthAwareQueryExecutor(innerExecutor, healthPolicy, options);
        var queryModel = QueryModel.Create(typeof(int)).WithTargetShards(targetShards);

        // act
        var results = new List<int>();
        await foreach (var item in executor.ExecuteAsync<int>(queryModel))
        {
            results.Add(item);
        }

        // assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Quarantine_Mode_Throws_When_Any_Shard_Unhealthy()
    {
        // arrange
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");
        var targetShards = new List<ShardId> { shard1, shard2 };

        var innerExecutor = new TestExecutor(model =>
        {
            return YieldRange(1, model.TargetShards?.Count ?? 0);
        });

        var healthPolicy = new TestHealthPolicy();
        healthPolicy.SetStatus(shard1, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard2, ShardHealthStatus.Unhealthy);

        var options = HealthAwareQueryOptions.Strict;
        var executor = new HealthAwareQueryExecutor(innerExecutor, healthPolicy, options);
        var queryModel = QueryModel.Create(typeof(int)).WithTargetShards(targetShards);

        // act/assert
        var exception = await Assert.ThrowsAsync<InsufficientHealthyShardsException>(async () =>
        {
            var results = new List<int>();
            await foreach (var item in executor.ExecuteAsync<int>(queryModel))
            {
                results.Add(item);
            }
        });

        exception.TotalShards.Should().Be(2);
        exception.HealthyShards.Should().Be(1);
        exception.UnhealthyShardIds.Should().ContainSingle().Which.Should().Be(shard2);
    }

    [Fact]
    public async Task RequireMinimum_Throws_When_Insufficient_Healthy_Shards()
    {
        // arrange
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");
        var shard3 = new ShardId("shard-3");
        var targetShards = new List<ShardId> { shard1, shard2, shard3 };

        var innerExecutor = new TestExecutor(model =>
        {
            return YieldRange(1, model.TargetShards?.Count ?? 0);
        });

        var healthPolicy = new TestHealthPolicy();
        healthPolicy.SetStatus(shard1, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard2, ShardHealthStatus.Unhealthy);
        healthPolicy.SetStatus(shard3, ShardHealthStatus.Unhealthy);

        var options = HealthAwareQueryOptions.RequireMinimum(2);
        var executor = new HealthAwareQueryExecutor(innerExecutor, healthPolicy, options);
        var queryModel = QueryModel.Create(typeof(int)).WithTargetShards(targetShards);

        // act/assert
        var exception = await Assert.ThrowsAsync<InsufficientHealthyShardsException>(async () =>
        {
            var results = new List<int>();
            await foreach (var item in executor.ExecuteAsync<int>(queryModel))
            {
                results.Add(item);
            }
        });

        exception.TotalShards.Should().Be(3);
        exception.HealthyShards.Should().Be(1);
    }

    [Fact]
    public async Task RequirePercentage_Succeeds_When_Threshold_Met()
    {
        // arrange
        var shard1 = new ShardId("shard-1");
        var shard2 = new ShardId("shard-2");
        var shard3 = new ShardId("shard-3");
        var shard4 = new ShardId("shard-4");
        var targetShards = new List<ShardId> { shard1, shard2, shard3, shard4 };

        var innerExecutor = new TestExecutor(model =>
        {
            return YieldRange(1, model.TargetShards?.Count ?? 0);
        });

        var healthPolicy = new TestHealthPolicy();
        healthPolicy.SetStatus(shard1, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard2, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard3, ShardHealthStatus.Healthy);
        healthPolicy.SetStatus(shard4, ShardHealthStatus.Unhealthy);

        var options = HealthAwareQueryOptions.RequirePercentage(0.75);
        var executor = new HealthAwareQueryExecutor(innerExecutor, healthPolicy, options);
        var queryModel = QueryModel.Create(typeof(int)).WithTargetShards(targetShards);

        // act
        var results = new List<int>();
        await foreach (var item in executor.ExecuteAsync<int>(queryModel))
        {
            results.Add(item);
        }

        // assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task No_Target_Shards_Executes_Normally()
    {
        // arrange
        var innerExecutor = new TestExecutor(model =>
        {
            return YieldRange(1, 5);
        });

        var healthPolicy = new TestHealthPolicy();
        var executor = new HealthAwareQueryExecutor(innerExecutor, healthPolicy, HealthAwareQueryOptions.BestEffort);
        var queryModel = QueryModel.Create(typeof(int));

        // act
        var results = new List<int>();
        await foreach (var item in executor.ExecuteAsync<int>(queryModel))
        {
            results.Add(item);
        }

        // assert
        results.Should().HaveCount(5);
    }

    private static async IAsyncEnumerable<int> YieldRange(int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return start + i;
        }
    }
}

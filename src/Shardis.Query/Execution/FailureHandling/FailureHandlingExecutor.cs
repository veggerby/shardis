using Shardis.Query.Execution.FailureHandling;

namespace Shardis.Query.Execution;

/// <summary>
/// Executor wrapper applying a shard failure handling strategy over an inner executor that may surface shard exceptions interleaved.
/// Strategy is invoked per underlying error.
/// </summary>
internal sealed class FailureHandlingExecutor : IShardQueryExecutor
{
    private readonly IShardQueryExecutor _inner;
    private readonly IShardQueryFailureStrategy _strategy;

    private readonly string _failureMode;

    // Reflection cached handles for unified latency emission (EF Core executor integration)
    private object? _efCoreExecutor;
    private System.Reflection.MethodInfo? _suppressMethod;
    private System.Reflection.MethodInfo? _consumePendingMethod;
    private System.Reflection.PropertyInfo? _metricsSinkProperty;
    private bool _suppressed;

    public FailureHandlingExecutor(IShardQueryExecutor inner, IShardQueryFailureStrategy strategy)
    {
        _inner = inner;
        _strategy = strategy;
        _failureMode = strategy switch
        {
            BestEffortFailureStrategy => "best-effort",
            _ => "fail-fast"
        };
    }

    public IShardQueryCapabilities Capabilities => _inner.Capabilities;

    // Failure mode inferred externally by checking wrapper type and underlying strategy instance.

    public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // For now assume inner already merges shards; to apply per-shard failure handling we need inner to surface annotated failures.
        // Interim implementation: treat any exception as from an unspecified shard index = -1.
        await foreach (var item in ExecuteWithFailureHandling<TResult>(model, ct))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<TResult> ExecuteWithFailureHandling<TResult>(QueryModel model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        IAsyncEnumerator<TResult>? e = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            FailureHandlingAmbient.CurrentMode.Value = _failureMode;
            TryEnableUnifiedLatencySuppression();
            e = _inner.ExecuteAsync<TResult>(model, ct).GetAsyncEnumerator(ct);
            while (true)
            {
                TResult current;
                try
                {
                    if (!await e.MoveNextAsync().ConfigureAwait(false))
                    {
                        yield break;
                    }
                    current = e.Current;
                }
                catch (Exception ex) when (Handle(ex))
                {
                    continue; // skip failed element
                }

                yield return current;
            }
        }
        finally
        {
            // Emit while ambient context still set so any downstream reflection (future) can observe it.
            if (e is not null)
            {
                await e.DisposeAsync().ConfigureAwait(false);
            }
            sw.Stop();
            EmitUnifiedLatencyIfSuppressed(sw.Elapsed.TotalMilliseconds, model, ct);
            FailureHandlingAmbient.CurrentMode.Value = null; // clear after emission
        }
    }

    private bool Handle(Exception ex)
    {
        return _strategy.OnShardException(ex, -1);
    }

    private void TryEnableUnifiedLatencySuppression()
    {
        // Walk inner wrappers to find EF executor instance by type name
        object? current = _inner;
        for (int i = 0; i < 4 && current is not null; i++)
        {
            var t = current.GetType();
            if (t.FullName?.Contains("EntityFrameworkCoreShardQueryExecutor", StringComparison.Ordinal) == true)
            {
                _efCoreExecutor = current;
                break;
            }
            var innerField = t.GetField("_inner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            current = innerField?.GetValue(current);
        }
        if (_efCoreExecutor is null) { return; }
        _suppressMethod = _efCoreExecutor.GetType().GetMethod("SuppressNextLatencyEmission", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _consumePendingMethod = _efCoreExecutor.GetType().GetMethod("ConsumePendingLatencyContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _metricsSinkProperty = _efCoreExecutor.GetType().GetProperty("MetricsSink", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (_suppressMethod is not null && _consumePendingMethod is not null && _metricsSinkProperty is not null)
        {
            try
            {
                _suppressMethod.Invoke(_efCoreExecutor, Array.Empty<object?>());
                _suppressed = true;
            }
            catch { _suppressed = false; }
        }
    }

    private void EmitUnifiedLatencyIfSuppressed(double elapsedMs, QueryModel model, CancellationToken ct)
    {
        if (!_suppressed || _efCoreExecutor is null || _consumePendingMethod is null || _metricsSinkProperty is null) { return; }
        try
        {
            var pending = _consumePendingMethod.Invoke(_efCoreExecutor, Array.Empty<object?>());
            if (pending is null) { return; }
            var pType = pending.GetType();
            string GetString(string name) => pType.GetProperty(name)?.GetValue(pending)?.ToString() ?? string.Empty;
            int GetInt(string name) => int.TryParse(pType.GetProperty(name)?.GetValue(pending)?.ToString(), out var v) ? v : 0;
            var dbSystem = GetString("dbSystem");
            var shardCount = GetInt("shardCount");
            var targetShardCount = GetInt("targetShardCount");
            var effectiveFanout = GetInt("effectiveFanout");
            var channelCapacity = GetInt("channelCapacity");
            var baseFailureMode = GetString("failureMode");
            var resultStatus = GetString("resultStatus");
            var rootType = GetString("rootType");
            var invalidShardCount = GetInt("invalidShardCount");

            if (_failureMode == "best-effort" && resultStatus == "failed")
            {
                resultStatus = "ok"; // partial success counts as ok
            }
            // Capture ambient failure mode (set by ExecuteWithFailureHandling) BEFORE it is cleared by caller finally.
            // Use wrapper's declared failure mode for determinism (ambient may already be cleared in future refactors).
            var failureMode = _failureMode;
            var metricsSink = _metricsSinkProperty.GetValue(_efCoreExecutor);
            var recordMethod = metricsSink?.GetType().GetMethod("RecordQueryMergeLatency");
            if (recordMethod is null) { return; }
            var tagsValue = Activator.CreateInstance(typeof(Shardis.Query.Diagnostics.QueryMetricTags),
                dbSystem,
                "efcore",
                shardCount,
                targetShardCount,
                "unordered",
                "false",
                effectiveFanout,
                channelCapacity,
                failureMode,
                resultStatus,
                rootType,
                invalidShardCount);
            recordMethod.Invoke(metricsSink, new object?[] { elapsedMs, tagsValue });
        }
        catch
        {
            // swallow
        }
        finally
        {
            _suppressed = false;
        }
    }
}

internal static class FailureHandlingAmbient
{
    public static AsyncLocal<string?> CurrentMode { get; } = new();
}
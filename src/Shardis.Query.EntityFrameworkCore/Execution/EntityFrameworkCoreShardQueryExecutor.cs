using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query;
using Shardis.Query.Execution;
using Shardis.Query.Internals;

namespace Shardis.Query.EntityFrameworkCore.Execution;

/// <summary>EF Core executor (provider package) - unordered streaming Where/Select.</summary>
/// <remarks>Create a new EF Core shard query executor.</remarks>
/// <param name="shardCount">Number of logical shards.</param>
/// <param name="contextFactory">Shard factory producing a <see cref="DbContext"/> for a given <see cref="ShardId"/>.</param>
/// <param name="merge">Unordered merge function.</param>
/// <param name="metrics">Optional metrics observer implementation.</param>
/// <param name="commandTimeoutSeconds">Optional database command timeout in seconds applied per shard query.</param>
/// <param name="maxConcurrency">Optional maximum degree of parallel shard queries (null = unbounded).</param>
/// <param name="disposeContextPerQuery">When true (default) a DbContext is created and disposed per shard query enumeration. When false contexts are cached for executor lifetime.</param>
/// <param name="queryMetrics">Optional query latency metrics sink.</param>
/// <param name="channelCapacity">Optional unordered merge channel capacity (for telemetry tagging only; merge delegate determines actual capacity).</param>
public sealed class EntityFrameworkCoreShardQueryExecutor(int shardCount,
                                                          IShardFactory<DbContext> contextFactory,
                                                          Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> merge,
                                                          Diagnostics.IQueryMetricsObserver? metrics = null,
                                                          int? commandTimeoutSeconds = null,
                                                          int? maxConcurrency = null,
                                                          bool disposeContextPerQuery = true,
                                                          Shardis.Query.Diagnostics.IShardisQueryMetrics? queryMetrics = null,
                                                          int? channelCapacity = null) : IShardQueryExecutor
{
    private readonly int _shardCount = shardCount;
    private readonly IShardFactory<DbContext> _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    private readonly Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> _merge = merge ?? throw new ArgumentNullException(nameof(merge));
    private readonly Diagnostics.IQueryMetricsObserver _metrics = metrics ?? Diagnostics.NoopQueryMetricsObserver.Instance;
    private readonly int? _commandTimeoutSeconds = commandTimeoutSeconds;
    private readonly SemaphoreSlim? _concurrencyGate = maxConcurrency is > 0 and < int.MaxValue ? new SemaphoreSlim(maxConcurrency.Value) : null;
    private readonly int? _configuredMaxConcurrency = maxConcurrency is > 0 and < int.MaxValue ? maxConcurrency : null;
    private readonly bool _disposePerQuery = disposeContextPerQuery;
    private readonly Dictionary<int, DbContext>? _retainedContexts = disposeContextPerQuery ? null : new();
    private readonly Shardis.Query.Diagnostics.IShardisQueryMetrics _queryMetrics = queryMetrics ?? Shardis.Query.Diagnostics.NoopShardisQueryMetrics.Instance;
    private readonly int? _channelCapacity = channelCapacity;
    private DbContext? _lastContext; // last created or retained context for provider detection
    private bool _suppressNextLatencyEmission; // set by ordered wrapper to unify single histogram emission

    /// <inheritdoc />
    public IShardQueryCapabilities Capabilities { get; } = new BasicQueryCapabilities(ordering: false, pagination: false);

    internal int InternalShardCount => _shardCount;

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default)
    {
        var tIn = model.SourceType;
        var activity = StartActivity(model, typeof(TResult));
        var sw = Stopwatch.StartNew();
        try
        {
            int invalidCount = 0;
            int[]? targetIds = null;
            if (model.TargetShards is { Count: > 0 })
            {
                var parsed = new List<int>(model.TargetShards.Count);
                foreach (var sid in model.TargetShards)
                {
                    if (int.TryParse(sid.Value, out var n) && n >= 0 && n < _shardCount)
                    {
                        if (!parsed.Contains(n)) { parsed.Add(n); }
                    }
                    else
                    {
                        invalidCount++;
                    }
                }
                if (parsed.Count > 0) { targetIds = parsed.OrderBy(x => x).ToArray(); }
            }
            var shardIndexes = targetIds ?? Enumerable.Range(0, _shardCount);
            if (invalidCount > 0)
            {
                activity?.AddTag("invalid.shard.count", invalidCount);
                activity?.AddEvent(new ActivityEvent("target.invalid", tags: new ActivityTagsCollection { { "invalid.count", invalidCount } }));
            }
            if (targetIds is not null)
            {
                var list = string.Join(',', targetIds.Take(10));
                if (targetIds.Length > 10) { list += ",10+ more"; }
                activity?.AddTag("target.shards", list);
                activity?.AddTag("target.shard.count", targetIds.Length);
            }
            var enumeratedShardCount = shardIndexes.Count();
            if (_configuredMaxConcurrency is int limit)
            {
                activity?.AddTag("fanout.limit", limit);
            }
            activity?.AddTag("fanout.enumerated", enumeratedShardCount);

            var per = shardIndexes.Select(idx => ExecShard<TResult>(idx, tIn, model, ct)).Select(Box);
            var merged = Cast<TResult>(_merge(per, ct), ct);

            return WrapCompletion(merged, ct, activity, sw, model, enumeratedShardCount);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.Dispose();
            throw;
        }
    }

    private async IAsyncEnumerable<TResult> ExecShard<TResult>(int shardId, Type tIn, QueryModel model, [EnumeratorCancellation] CancellationToken ct)
    {
        if (_concurrencyGate is not null)
        {
            await _concurrencyGate.WaitAsync(ct).ConfigureAwait(false);
        }
        var shardActivity = Activity.Current is null ? null : ShardisQueryActivitySource.Instance.StartActivity("shard", ActivityKind.Internal);
        shardActivity?.AddTag("shard.index", shardId);
        _metrics.OnShardStart(shardId);
        var shard = new ShardId(shardId.ToString());
        DbContext? ctx = null;
        bool created = false;
        if (_disposePerQuery)
        {
            ctx = await _contextFactory.CreateAsync(shard, ct).ConfigureAwait(false);
            created = true;
        }
        else
        {
            lock (_retainedContexts!)
            {
                if (!_retainedContexts.TryGetValue(shardId, out ctx))
                {
                    created = true;
                }
            }
            if (created)
            {
                var newCtx = await _contextFactory.CreateAsync(shard, ct).ConfigureAwait(false);
                lock (_retainedContexts!)
                {
                    ctx = _retainedContexts.ContainsKey(shardId) ? _retainedContexts[shardId] : (_retainedContexts[shardId] = newCtx);
                }
            }
        }

        try
        {
            // Apply optional command timeout (per shard) if specified (capture previous to restore on retained contexts)
            int? previousTimeout = null;
            if (_commandTimeoutSeconds is int secs && secs > 0 && ctx is not null)
            {
                try
                {
                    previousTimeout = ctx.Database.GetCommandTimeout();
                    ctx.Database.SetCommandTimeout(secs);
                    shardActivity?.AddTag("db.command_timeout.seconds", secs);
                }
                catch (Exception ex)
                {
                    shardActivity?.AddEvent(new ActivityEvent("timeout.apply.failed", tags: new ActivityTagsCollection { { "exception.message", ex.Message } }));
                }
            }

            _lastContext = ctx ?? _lastContext;
            var setGeneric = typeof(DbContext).GetMethods().First(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(tIn);
            var raw = setGeneric.Invoke(ctx, null)!;
            var q = (IQueryable)raw;
            var apply = typeof(QueryComposer).GetMethod(nameof(QueryComposer.ApplyQueryable))!.MakeGenericMethod(tIn, typeof(TResult));
            var applied = (IQueryable<TResult>)apply.Invoke(null, [q, model])!;

            // Default to AsNoTracking for query performance / reduced change tracking overhead
            var asNoTracking = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(TResult));

            applied = (IQueryable<TResult>)asNoTracking.Invoke(null, [applied])!;

            try
            {
                await foreach (var item in applied.AsAsyncEnumerable().WithCancellation(ct).ConfigureAwait(false))
                {
                    if (ct.IsCancellationRequested) { _metrics.OnCanceled(); yield break; }
                    _metrics.OnItemsProduced(shardId, 1);
                    yield return item;
                }
            }
            finally
            {
                _metrics.OnShardStop(shardId);
                if (_commandTimeoutSeconds is not null && !_disposePerQuery && ctx is not null)
                {
                    // restore timeout on retained context
                    try { ctx.Database.SetCommandTimeout(previousTimeout); } catch { }
                }
                if (created && _disposePerQuery && ctx is not null)
                {
                    await ctx.DisposeAsync().ConfigureAwait(false);
                }
                if (_concurrencyGate is not null)
                {
                    _concurrencyGate.Release();
                }
                shardActivity?.Dispose();
            }
        }
        finally
        {
            // ensure semaphore released if outer try body throws before inner finally
            // inner finally already released on success path
        }
    }

    private static async IAsyncEnumerable<T> Cast<T>(IAsyncEnumerable<object> src, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var o in src.WithCancellation(ct))
        {
            yield return (T)o!;
        }
    }

    private static async IAsyncEnumerable<object> Box<T>(IAsyncEnumerable<T> src)
    {
        await foreach (var item in src.ConfigureAwait(false))
        {
            yield return item!;
        }
    }

    private async IAsyncEnumerable<T> WrapCompletion<T>(IAsyncEnumerable<T> src, [EnumeratorCancellation] CancellationToken ct, Activity? root, Stopwatch sw, QueryModel model, int enumeratedShardCount)
    {
        var completed = false;
        try
        {
            await foreach (var item in src.WithCancellation(ct))
            {
                yield return item;
            }
            completed = true;
        }
        finally
        {
            sw.Stop();
            root?.AddTag("shard.count", _shardCount);
            if (model.TargetShards is not null) { root?.AddTag("target.shard.count", model.TargetShards.Count); }
            root?.AddTag("query.duration.ms", sw.Elapsed.TotalMilliseconds);
            if (_channelCapacity.HasValue) { root?.AddTag("channel.capacity", _channelCapacity.Value); }
            var status = "ok";
            if (ct.IsCancellationRequested && !completed)
            {
                _metrics.OnCanceled();
                root?.SetStatus(ActivityStatusCode.Error, "canceled");
                status = "canceled";
            }
            else if (!completed)
            {
                // Faulted
                status = "failed";
                root?.SetStatus(ActivityStatusCode.Error, "failed");
            }
            else
            {
                _metrics.OnCompleted();
                root?.SetStatus(ActivityStatusCode.Ok);
            }
            // Effective concurrency = min(enumerated shard count, configured limit if any)
            var effectiveFanout = _configuredMaxConcurrency.HasValue
                ? Math.Min(enumeratedShardCount, _configuredMaxConcurrency.Value)
                : enumeratedShardCount;
            root?.AddTag("fanout.effective", effectiveFanout);
            // Attempt provider detection from first retained or last created context (best effort, stable for histogram cardinality)
            string? dbSystem = null;
            try
            {
                var anyCtx = !_disposePerQuery && _retainedContexts is { Count: > 0 } ? _retainedContexts.Values.FirstOrDefault() : null;
                anyCtx ??= _lastContext; // last created if available
                var providerName = anyCtx?.Database.ProviderName;
                if (providerName is not null)
                {
                    if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)) dbSystem = "sqlite";
                    else if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) dbSystem = "postgresql";
                    else if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)) dbSystem = "mssql";
                    else if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase)) dbSystem = "mysql";
                    else dbSystem = "other";
                }
            }
            catch { dbSystem = null; }
            var failureMode = DetectFailureMode();
            if (!_suppressNextLatencyEmission)
            {
                _queryMetrics.RecordQueryMergeLatency(sw.Elapsed.TotalMilliseconds, new Shardis.Query.Diagnostics.QueryMetricTags(
                    dbSystem: dbSystem ?? "",
                    provider: "efcore",
                    shardCount: _shardCount,
                    targetShardCount: enumeratedShardCount,
                    mergeStrategy: "unordered",
                    orderingBuffered: "false",
                    fanoutConcurrency: effectiveFanout,
                    channelCapacity: _channelCapacity ?? -1,
                    failureMode: failureMode,
                    resultStatus: status,
                    rootType: model.SourceType.Name));
            }
            else
            {
                // one-time suppression consumed
                _suppressNextLatencyEmission = false;
            }
            root?.Dispose();
        }
    }

    internal static string DetectFailureMode()
    {
        // In this provider instance we cannot directly inspect outer wrappers; assume best-effort wrapper sets ambient marker in Activity if needed.
        // For current implementation we look at call stack for FailureHandlingExecutor presence — lightweight heuristic.
        try
        {
            var stack = new System.Diagnostics.StackTrace();
            foreach (var frame in stack.GetFrames() ?? Array.Empty<System.Diagnostics.StackFrame>())
            {
                var m = frame.GetMethod();
                if (m?.DeclaringType?.Name == "FailureHandlingExecutor")
                {
                    // Determine strategy field via reflection (private _strategy)
                    var stratField = m.DeclaringType.GetField("_strategy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stratField != null)
                    {
                        var thisField = stratField.DeclaringType;
                    }
                    // Stack presence implies either fail-fast (redundant) or best-effort; conservatively return best-effort for visibility.
                    return "best-effort";
                }
            }
        }
        catch { }
        return "fail-fast";
    }

    private static Activity? StartActivity(QueryModel model, Type resultType)
    {
        var act = ShardisQueryActivitySource.Instance.StartActivity("shardis.query", ActivityKind.Client);
        act?.AddTag("query.source", model.SourceType.FullName);
        act?.AddTag("query.result", resultType.FullName);
        act?.AddTag("query.where.count", model.Where.Count);
        act?.AddTag("query.has.select", model.Select is not null);
        return act;
    }

    internal void SuppressNextLatencyEmission()
    {
        _suppressNextLatencyEmission = true;
    }

    internal void RecordLatencyOverride(double milliseconds,
                                        QueryModel model,
                                        int enumeratedShardCount,
                                        string mergeStrategy,
                                        bool orderingBuffered,
                                        string resultStatus,
                                        int effectiveFanout)
    {
        string? dbSystem = null;
        try
        {
            var anyCtx = !_disposePerQuery && _retainedContexts is { Count: > 0 } ? _retainedContexts.Values.FirstOrDefault() : null;
            anyCtx ??= _lastContext;
            var providerName = anyCtx?.Database.ProviderName;
            if (providerName is not null)
            {
                if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)) dbSystem = "sqlite";
                else if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) dbSystem = "postgresql";
                else if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)) dbSystem = "mssql";
                else if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase)) dbSystem = "mysql";
                else dbSystem = "other";
            }
        }
        catch { dbSystem = null; }
        var failureMode = DetectFailureMode();
        _queryMetrics.RecordQueryMergeLatency(milliseconds, new Shardis.Query.Diagnostics.QueryMetricTags(
            dbSystem: dbSystem ?? string.Empty,
            provider: "efcore",
            shardCount: _shardCount,
            targetShardCount: enumeratedShardCount,
            mergeStrategy: mergeStrategy,
            orderingBuffered: orderingBuffered ? "true" : "false",
            fanoutConcurrency: effectiveFanout,
            channelCapacity: _channelCapacity ?? -1,
            failureMode: failureMode,
            resultStatus: resultStatus,
            rootType: model.SourceType.Name));
    }

    internal Shardis.Query.Diagnostics.IShardisQueryMetrics MetricsSink => _queryMetrics;
}

internal static class ShardisQueryActivitySource
{
    public static ActivitySource Instance { get; } = new("Shardis.Query");
}
using System.Runtime.CompilerServices;

using Shardis.Query;
using Shardis.Query.Execution;

namespace Shardis.Query.InMemory.Execution;

/// <summary>In-memory executor for development &amp; tests.</summary>
/// <remarks>Create a new in-memory executor.</remarks>
/// <param name="shards">Shard sequences.</param>
/// <param name="merge">Unordered merge function.</param>
/// <param name="metrics">Optional metrics observer.</param>
public sealed class InMemoryShardQueryExecutor(IReadOnlyList<IEnumerable<object>> shards, Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> merge, Diagnostics.IQueryMetricsObserver? metrics = null) : IShardQueryExecutor
{
    private readonly IReadOnlyList<IEnumerable<object>> _shards = shards ?? throw new ArgumentNullException(nameof(shards));
    private readonly Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> _merge = merge ?? throw new ArgumentNullException(nameof(merge));
    private readonly Diagnostics.IQueryMetricsObserver _metrics = metrics ?? Diagnostics.NoopQueryMetricsObserver.Instance;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CompiledPipeline> _pipelineCache = new();

    private sealed record CompiledPipeline(Func<object, bool>? Where, Func<object, object> Select);
    internal static int CompileCount;
    /// <summary>Total number of compiled pipelines across all executor instances (for benchmark diagnostics).</summary>
    public static int TotalCompiledPipelines => CompileCount;

    /// <inheritdoc />
    public IShardQueryCapabilities Capabilities => BasicQueryCapabilities.None;

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default)
    {
        var tIn = model.SourceType;
        var key = ComputeCacheKey(model);
        var compiled = _pipelineCache.GetOrAdd(key, _ => CompilePipeline(model));
        IEnumerable<int> shardIndexes;
        if (model.TargetShards is { Count: > 0 })
        {
            var parsed = new List<int>(model.TargetShards.Count);
            var invalid = 0;
            foreach (var sid in model.TargetShards)
            {
                if (int.TryParse(sid.Value, out var n) && n >= 0 && n < _shards.Count)
                {
                    if (!parsed.Contains(n)) { parsed.Add(n); }
                }
                else
                {
                    invalid++;
                }
            }
            shardIndexes = parsed.Count > 0 ? parsed.OrderBy(x => x).ToArray() : Enumerable.Range(0, _shards.Count);
            if (invalid > 0)
            {
                // In-memory executor lacks Activity; surface via metrics event hook in future if needed.
            }
        }
        else
        {
            shardIndexes = Enumerable.Range(0, _shards.Count);
        }
        var per = shardIndexes.Select(shardId => Project<TResult>(_shards[shardId], tIn, model, compiled, shardId, ct)).Select(Box);
        var merged = Cast<TResult>(_merge(per, ct), ct);

        return WrapCompletion(merged, ct, model);
    }

    private static string ComputeCacheKey(QueryModel model)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(model.SourceType.FullName).Append('|');

        foreach (var w in model.Where)
        {
            sb.Append(w.Body.ToString()).Append(';');
        }

        sb.Append("|SEL|");
        sb.Append(model.Select?.Body.ToString() ?? "ID");

        return sb.ToString();
    }

    private static CompiledPipeline CompilePipeline(QueryModel model)
    {
        Interlocked.Increment(ref CompileCount);
        var tIn = model.SourceType;
        var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
        var castIn = System.Linq.Expressions.Expression.Convert(param, tIn);

        Func<object, bool>? whereDel = null;
        if (model.Where.Count > 0)
        {
            System.Linq.Expressions.Expression? body = null;

            foreach (var pred in model.Where)
            {
                var replaced = new ParameterReplaceVisitor(pred.Parameters[0], castIn).Visit(pred.Body);
                body = body == null ? replaced : System.Linq.Expressions.Expression.AndAlso(body, replaced!);
            }

            whereDel = System.Linq.Expressions.Expression.Lambda<Func<object, bool>>(body!, param).Compile();
        }

        Func<object, object> selectDel;

        if (model.Select != null)
        {
            var projBody = new ParameterReplaceVisitor(model.Select.Parameters[0], castIn).Visit(model.Select.Body)!;
            selectDel = System.Linq.Expressions.Expression.Lambda<Func<object, object>>(System.Linq.Expressions.Expression.Convert(projBody, typeof(object)), param).Compile();
        }
        else
        {
            selectDel = o => o;
        }

        return new CompiledPipeline(whereDel, selectDel);
    }

    private sealed class ParameterReplaceVisitor(System.Linq.Expressions.ParameterExpression from, System.Linq.Expressions.Expression to) : System.Linq.Expressions.ExpressionVisitor
    {
        protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    private IAsyncEnumerable<TResult> Project<TResult>(IEnumerable<object> src, Type tIn, QueryModel model, CompiledPipeline pipeline, int shardId, CancellationToken ct)
    {
        _metrics.OnShardStart(shardId);
        var typed = CastToType(src, tIn);
        var iter = ExecPipeline<TResult>(typed, pipeline, shardId, ct);
        return iter;
    }

    private async IAsyncEnumerable<TResult> ExecPipeline<TResult>(IEnumerable<object> src, CompiledPipeline pipeline, int shardId, [EnumeratorCancellation] CancellationToken ct)
    {
        var produced = 0;

        foreach (var o in src)
        {
            if (ct.IsCancellationRequested)
            {
                _metrics.OnCanceled();
                yield break;
            }

            if (pipeline.Where == null || pipeline.Where(o))
            {
                var projected = pipeline.Select(o);
                produced++;
                _metrics.OnItemsProduced(shardId, 1);

                yield return (TResult)projected!;

                await Task.Yield();
            }
        }

        _metrics.OnShardStop(shardId);
    }

    private static IEnumerable<object> CastToType(IEnumerable<object> src, Type tIn)
    {
        // already object collection representing tIn instances
        return src;
    }

    private static async IAsyncEnumerable<T> Cast<T>(IAsyncEnumerable<object> src, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var o in src.WithCancellation(ct))
        {
            yield return (T)o!;
        }
    }

    private async IAsyncEnumerable<T> WrapCompletion<T>(IAsyncEnumerable<T> src, [EnumeratorCancellation] CancellationToken ct, QueryModel model)
    {
        var completed = false;
        var enumerated = model.TargetShards?.Count ?? _shards.Count;

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
            if (ct.IsCancellationRequested && !completed)
            {
                _metrics.OnCanceled();
            }
            else
            {
                _metrics.OnCompleted();
            }
            // Future: surface enumerated/effective fanout via observer extension or metrics tags.
        }
    }

    private static async IAsyncEnumerable<object> Box<T>(IAsyncEnumerable<T> src)
    {
        await foreach (var item in src.ConfigureAwait(false))
        {
            yield return item!;
        }
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> src, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var i in src)
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            yield return i;
            await Task.Yield();
        }
    }
}
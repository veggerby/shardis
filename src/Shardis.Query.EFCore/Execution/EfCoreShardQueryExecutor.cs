using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Shardis.Query.Internals;

namespace Shardis.Query.Execution.EFCore;

/// <summary>EF Core executor (provider package) – unordered streaming Where/Select.</summary>
public sealed class EfCoreShardQueryExecutor : IShardQueryExecutor
{
    private readonly int _shardCount;
    private readonly Func<int, DbContext> _contextFactory;
    private readonly Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> _merge;
    private readonly IShardQueryCapabilities _caps = new BasicQueryCapabilities(ordering: false, pagination: false);
    private readonly Shardis.Query.Diagnostics.IQueryMetricsObserver _metrics;
    private readonly int? _commandTimeoutSeconds;

    /// <summary>Create a new EF Core shard query executor.</summary>
    /// <param name="shardCount">Number of logical shards.</param>
    /// <param name="contextFactory">Factory giving a DbContext for a shard id.</param>
    /// <param name="merge">Unordered merge function.</param>
    /// <param name="metrics">Optional metrics observer implementation.</param>
    /// <param name="commandTimeoutSeconds">Optional database command timeout in seconds applied per shard query.</param>
    public EfCoreShardQueryExecutor(int shardCount, Func<int, DbContext> contextFactory, Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> merge, Shardis.Query.Diagnostics.IQueryMetricsObserver? metrics = null, int? commandTimeoutSeconds = null)
    {
        _shardCount = shardCount;
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _merge = merge ?? throw new ArgumentNullException(nameof(merge));
        _metrics = metrics ?? Shardis.Query.Diagnostics.NoopQueryMetricsObserver.Instance;
        _commandTimeoutSeconds = commandTimeoutSeconds;
    }

    /// <inheritdoc />
    public IShardQueryCapabilities Capabilities => _caps;

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default)
    {
        var tIn = model.SourceType;
        var per = Enumerable.Range(0, _shardCount).Select(id => ExecShard<TResult>(id, tIn, model, ct)).Select(Box);
        var merged = Cast<TResult>(_merge(per, ct), ct);
        return WrapCompletion(merged, ct);
    }

    private async IAsyncEnumerable<TResult> ExecShard<TResult>(int shardId, Type tIn, QueryModel model, [EnumeratorCancellation] CancellationToken ct)
    {
        _metrics.OnShardStart(shardId);
        await using var ctx = _contextFactory(shardId);
        // Apply optional command timeout (per shard) if specified.
        if (_commandTimeoutSeconds is int secs && secs > 0)
        {
            try
            {
                ctx.Database.SetCommandTimeout(secs);
            }
            catch (Exception)
            {
                // Defensive: if provider does not support setting timeout, continue without failing the whole query.
            }
        }
        var setGeneric = typeof(DbContext).GetMethods().First(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(tIn);
        var raw = setGeneric.Invoke(ctx, null)!;
        var q = (IQueryable)raw;
        var apply = typeof(QueryComposer).GetMethod(nameof(QueryComposer.ApplyQueryable))!.MakeGenericMethod(tIn, typeof(TResult));
        var applied = (IQueryable<TResult>)apply.Invoke(null, new object[] { q, model })!;
        // Default to AsNoTracking for query performance / reduced change tracking overhead
        var asNoTracking = typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .First(m => m.Name == nameof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking) && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(TResult));
        applied = (IQueryable<TResult>)asNoTracking.Invoke(null, new object[] { applied })!;
        var produced = 0;
        try
        {
            await foreach (var item in applied.AsAsyncEnumerable().WithCancellation(ct).ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) { _metrics.OnCanceled(); yield break; }
                produced++;
                _metrics.OnItemsProduced(shardId, 1);
                yield return item;
            }
        }
        finally
        {
            _metrics.OnShardStop(shardId);
        }
    }

    private static async IAsyncEnumerable<T> Cast<T>(IAsyncEnumerable<object> src, [EnumeratorCancellation] CancellationToken ct)
    { await foreach (var o in src.WithCancellation(ct)) { yield return (T)o!; } }

    private static async IAsyncEnumerable<object> Box<T>(IAsyncEnumerable<T> src)
    {
        await foreach (var item in src.ConfigureAwait(false)) { yield return item!; }
    }

    private async IAsyncEnumerable<T> WrapCompletion<T>(IAsyncEnumerable<T> src, [EnumeratorCancellation] CancellationToken ct)
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
            if (ct.IsCancellationRequested && !completed)
            {
                _metrics.OnCanceled();
            }
            else
            {
                _metrics.OnCompleted();
            }
        }
    }
}
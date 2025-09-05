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
public sealed class EntityFrameworkCoreShardQueryExecutor(int shardCount, IShardFactory<DbContext> contextFactory, Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> merge, Diagnostics.IQueryMetricsObserver? metrics = null, int? commandTimeoutSeconds = null) : IShardQueryExecutor
{
    private readonly int _shardCount = shardCount;
    private readonly IShardFactory<DbContext> _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    private readonly Func<IEnumerable<IAsyncEnumerable<object>>, CancellationToken, IAsyncEnumerable<object>> _merge = merge ?? throw new ArgumentNullException(nameof(merge));
    private readonly Diagnostics.IQueryMetricsObserver _metrics = metrics ?? Diagnostics.NoopQueryMetricsObserver.Instance;
    private readonly int? _commandTimeoutSeconds = commandTimeoutSeconds;

    /// <inheritdoc />
    public IShardQueryCapabilities Capabilities { get; } = new BasicQueryCapabilities(ordering: false, pagination: false);

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryModel model, CancellationToken ct = default)
    {
        var tIn = model.SourceType;
        var per = Enumerable.Range(0, _shardCount).Select(idx => ExecShard<TResult>(idx, tIn, model, ct)).Select(Box);
        var merged = Cast<TResult>(_merge(per, ct), ct);

        return WrapCompletion(merged, ct);
    }

    private async IAsyncEnumerable<TResult> ExecShard<TResult>(int shardId, Type tIn, QueryModel model, [EnumeratorCancellation] CancellationToken ct)
    {
        _metrics.OnShardStart(shardId);
        var shard = new ShardId(shardId.ToString());
        await using var ctx = await _contextFactory.CreateAsync(shard, ct).ConfigureAwait(false);

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
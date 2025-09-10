using System.Linq.Expressions;

using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// High-level, developer-friendly entry point for initiating shard queries.
/// </summary>
/// <remarks>
/// This interface layers ergonomic helpers on top of the lower-level <see cref="IShardQueryExecutor"/>:
/// <list type="bullet">
/// <item><description>Strongly-typed <c>Query&lt;T&gt;()</c> bootstrap without calling <c>ShardQuery.For&lt;T&gt;()</c>.</description></item>
/// <item><description>Combined <c>Query&lt;T,TResult&gt;(...)</c> allowing optional inline <c>where</c> + <c>select</c> composition.</description></item>
/// <item><description>Deferred execution – no shard fan-out occurs until enumeration / terminal operator.</description></item>
/// </list>
/// Guidelines:
/// <list type="bullet">
/// <item><description>Provide a projection when the result type differs from the source type.</description></item>
/// <item><description>Use terminal helpers (<c>AnyAsync</c>, <c>CountAsync</c>, <c>FirstOrDefaultAsync</c>) from <see cref="ShardQueryableTerminalExtensions"/> for common aggregate patterns.</description></item>
/// </list>
/// </remarks>
public interface IShardQueryClient
{
    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/>.
    /// </summary>
    IShardQueryable<T> Query<T>();

    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/> applying optional filter and projection.
    /// A projection must be supplied when <typeparamref name="TResult"/> differs from <typeparamref name="T"/>.
    /// </summary>
    IShardQueryable<TResult> Query<T, TResult>(Expression<Func<T, bool>>? where = null,
                                               Expression<Func<T, TResult>>? select = null);
}

using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Health;
using Shardis.Model;

namespace Shardis.Query.EntityFrameworkCore.Health;

/// <summary>
/// Entity Framework Core-specific health probe that checks database connectivity.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
public sealed class EfCoreShardHealthProbe<TContext> : IShardHealthProbe where TContext : DbContext
{
    private readonly IShardFactory<TContext> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreShardHealthProbe{TContext}"/> class.
    /// </summary>
    /// <param name="factory">The shard factory for creating DbContext instances.</param>
    public EfCoreShardHealthProbe(IShardFactory<TContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public async ValueTask<ShardHealthReport> ExecuteAsync(ShardId shardId, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var context = await _factory.CreateAsync(shardId, ct).ConfigureAwait(false);
            
            var canConnect = await context.Database.CanConnectAsync(ct).ConfigureAwait(false);
            sw.Stop();

            return new ShardHealthReport
            {
                ShardId = shardId,
                Status = canConnect ? ShardHealthStatus.Healthy : ShardHealthStatus.Unhealthy,
                Timestamp = DateTimeOffset.UtcNow,
                Description = canConnect ? "Database connection successful" : "Cannot connect to database",
                ProbeDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ShardHealthReport
            {
                ShardId = shardId,
                Status = ShardHealthStatus.Unhealthy,
                Timestamp = DateTimeOffset.UtcNow,
                Description = $"Database health check failed: {ex.Message}",
                Exception = ex,
                ProbeDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }
    }
}

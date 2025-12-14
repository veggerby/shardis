using System.Collections.Concurrent;

using Shardis.Model;

namespace Shardis.Health;

/// <summary>
/// Default implementation of <see cref="IShardHealthPolicy"/> with periodic probing and threshold-based status transitions.
/// </summary>
/// <remarks>
/// <para>
/// This policy periodically probes shards using the configured <see cref="IShardHealthProbe"/>.
/// It tracks consecutive failures and successes to determine health status transitions based on thresholds.
/// Reactive tracking (recording operation results) is optionally supported.
/// </para>
/// <para>
/// Note: The internal state dictionary grows as new shards are discovered. In typical deployments
/// where the shard count is fixed and relatively small (tens to hundreds), this is acceptable.
/// For scenarios with dynamic or very large shard sets, consider providing the full shard list
/// upfront via the constructor or implementing a custom policy with eviction logic.
/// </para>
/// </remarks>
public sealed class PeriodicShardHealthPolicy : IShardHealthPolicy, IDisposable
{
    private readonly IShardHealthProbe _probe;
    private readonly ShardHealthPolicyOptions _options;
    private readonly Action<double, string, string>? _recordProbeLatency;
    private readonly Action<string>? _recordRecovery;
    private readonly ConcurrentDictionary<ShardId, ShardHealthState> _states = new();
    private readonly Timer? _timer;
    private readonly object _lock = new();
    private readonly CancellationTokenSource _disposalCts = new();
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicShardHealthPolicy"/> class.
    /// </summary>
    /// <param name="probe">The health probe to use for shard checks.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="shardIds">Optional collection of shard IDs to monitor. If null, shards are discovered reactively.</param>
    /// <param name="recordProbeLatency">Optional callback to record probe latency metrics (ms, shardId, status).</param>
    /// <param name="recordRecovery">Optional callback to record shard recovery events.</param>
    public PeriodicShardHealthPolicy(
        IShardHealthProbe probe,
        ShardHealthPolicyOptions? options = null,
        IEnumerable<ShardId>? shardIds = null,
        Action<double, string, string>? recordProbeLatency = null,
        Action<string>? recordRecovery = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _options = options ?? new ShardHealthPolicyOptions();
        _recordProbeLatency = recordProbeLatency;
        _recordRecovery = recordRecovery;

        if (shardIds is not null)
        {
            foreach (var id in shardIds)
            {
                _states.TryAdd(id, new ShardHealthState(id));
            }
        }

        if (_options.ProbeInterval > TimeSpan.Zero)
        {
            _timer = new Timer(PeriodicProbeCallback, null, _options.ProbeInterval, _options.ProbeInterval);
        }
    }

    /// <inheritdoc />
    public ValueTask<ShardHealthReport> GetHealthAsync(ShardId shardId, CancellationToken ct = default)
    {
        var state = _states.GetOrAdd(shardId, id => new ShardHealthState(id));
        return ValueTask.FromResult(state.GetReport());
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShardHealthReport> GetAllHealthAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var kvp in _states)
        {
            ct.ThrowIfCancellationRequested();
            yield return kvp.Value.GetReport();
        }
    }

    /// <inheritdoc />
    public ValueTask RecordSuccessAsync(ShardId shardId, CancellationToken ct = default)
    {
        if (!_options.ReactiveTrackingEnabled)
        {
            return ValueTask.CompletedTask;
        }

        var state = _states.GetOrAdd(shardId, id => new ShardHealthState(id));
        state.RecordSuccess(_options);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RecordFailureAsync(ShardId shardId, Exception exception, CancellationToken ct = default)
    {
        if (!_options.ReactiveTrackingEnabled)
        {
            return ValueTask.CompletedTask;
        }

        var state = _states.GetOrAdd(shardId, id => new ShardHealthState(id));
        state.RecordFailure(exception, _options);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ShardHealthReport> ProbeAsync(ShardId shardId, CancellationToken ct = default)
    {
        var state = _states.GetOrAdd(shardId, id => new ShardHealthState(id));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_options.ProbeTimeout);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var report = await _probe.ExecuteAsync(shardId, cts.Token).ConfigureAwait(false);
            sw.Stop();
            _recordProbeLatency?.Invoke(sw.Elapsed.TotalMilliseconds, shardId.Value, report.Status.ToString());
            
            var previousStatus = state.GetReport().Status;
            state.UpdateFromProbe(report, _options);
            var newReport = state.GetReport();
            
            if (previousStatus == ShardHealthStatus.Unhealthy && newReport.Status == ShardHealthStatus.Healthy)
            {
                _recordRecovery?.Invoke(shardId.Value);
            }
            
            return newReport;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _recordProbeLatency?.Invoke(sw.Elapsed.TotalMilliseconds, shardId.Value, "failed");
            state.RecordProbeFailure(ex, _options);
            return state.GetReport();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _timer?.Dispose();
        _disposalCts.Cancel();
        _disposalCts.Dispose();
        _disposed = true;
    }

    private void PeriodicProbeCallback(object? state)
    {
        if (_disposed || _disposalCts.IsCancellationRequested)
        {
            return;
        }

        foreach (var kvp in _states)
        {
            var shardId = kvp.Key;
            var shardState = kvp.Value;

            if (!shardState.ShouldProbe(_options))
            {
                continue;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProbeAsync(shardId, _disposalCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during disposal
                }
                catch (Exception ex)
                {
                    // Record probe failure for observability
                    _recordProbeLatency?.Invoke(0, shardId.Value, "failed");
                    
                    // Update state to reflect failure
                    try
                    {
                        var state = _states.GetOrAdd(shardId, id => new ShardHealthState(id));
                        state.RecordProbeFailure(ex, _options);
                    }
                    catch
                    {
                        // Swallow state update failures to prevent timer callback crashes
                    }
                }
            }, _disposalCts.Token);
        }
    }

    private sealed class ShardHealthState
    {
        private readonly ShardId _shardId;
        private ShardHealthStatus _status = ShardHealthStatus.Unknown;
        private int _consecutiveFailures;
        private int _consecutiveSuccesses;
        private DateTimeOffset _lastProbe = DateTimeOffset.MinValue;
        private DateTimeOffset _lastTransition = DateTimeOffset.UtcNow;
        private string? _description;
        private Exception? _exception;
        private double? _lastProbeDurationMs;
        private readonly object _lock = new();

        public ShardHealthState(ShardId shardId)
        {
            _shardId = shardId;
        }

        public bool ShouldProbe(ShardHealthPolicyOptions options)
        {
            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow;

                if (_status == ShardHealthStatus.Unhealthy)
                {
                    var timeSinceTransition = now - _lastTransition;
                    if (timeSinceTransition < options.CooldownPeriod)
                    {
                        return false;
                    }
                }

                var timeSinceLastProbe = now - _lastProbe;
                return timeSinceLastProbe >= options.ProbeInterval;
            }
        }

        public void RecordSuccess(ShardHealthPolicyOptions options)
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _consecutiveSuccesses++;
                _exception = null;

                if (_status == ShardHealthStatus.Unhealthy && _consecutiveSuccesses >= options.HealthyThreshold)
                {
                    TransitionTo(ShardHealthStatus.Healthy, "Recovered after consecutive successes");
                }
                else if (_status == ShardHealthStatus.Unknown)
                {
                    TransitionTo(ShardHealthStatus.Healthy, "First successful operation");
                }
            }
        }

        public void RecordFailure(Exception exception, ShardHealthPolicyOptions options)
        {
            lock (_lock)
            {
                _consecutiveSuccesses = 0;
                _consecutiveFailures++;
                _exception = exception;

                if (_status != ShardHealthStatus.Unhealthy && _consecutiveFailures >= options.UnhealthyThreshold)
                {
                    TransitionTo(ShardHealthStatus.Unhealthy, $"Exceeded failure threshold ({_consecutiveFailures} consecutive failures)");
                }
            }
        }

        public void UpdateFromProbe(ShardHealthReport report, ShardHealthPolicyOptions options)
        {
            lock (_lock)
            {
                _lastProbe = DateTimeOffset.UtcNow;
                _lastProbeDurationMs = report.ProbeDurationMs;

                if (report.Status == ShardHealthStatus.Healthy)
                {
                    _consecutiveFailures = 0;
                    _consecutiveSuccesses++;
                    _exception = null;

                    if (_status == ShardHealthStatus.Unhealthy && _consecutiveSuccesses >= options.HealthyThreshold)
                    {
                        TransitionTo(ShardHealthStatus.Healthy, "Probe successful after recovery");
                    }
                    else if (_status != ShardHealthStatus.Healthy)
                    {
                        TransitionTo(ShardHealthStatus.Healthy, "Probe successful");
                    }
                }
                else
                {
                    _consecutiveSuccesses = 0;
                    _consecutiveFailures++;
                    _exception = report.Exception;
                    _description = report.Description;

                    if (_status != ShardHealthStatus.Unhealthy && _consecutiveFailures >= options.UnhealthyThreshold)
                    {
                        TransitionTo(ShardHealthStatus.Unhealthy, report.Description ?? "Probe failed");
                    }
                }
            }
        }

        public void RecordProbeFailure(Exception exception, ShardHealthPolicyOptions options)
        {
            lock (_lock)
            {
                _lastProbe = DateTimeOffset.UtcNow;
                _consecutiveSuccesses = 0;
                _consecutiveFailures++;
                _exception = exception;

                if (_status != ShardHealthStatus.Unhealthy && _consecutiveFailures >= options.UnhealthyThreshold)
                {
                    TransitionTo(ShardHealthStatus.Unhealthy, $"Probe failed: {exception.Message}");
                }
            }
        }

        public ShardHealthReport GetReport()
        {
            lock (_lock)
            {
                return new ShardHealthReport
                {
                    ShardId = _shardId,
                    Status = _status,
                    Timestamp = DateTimeOffset.UtcNow,
                    Description = _description,
                    Exception = _exception,
                    ProbeDurationMs = _lastProbeDurationMs
                };
            }
        }

        private void TransitionTo(ShardHealthStatus newStatus, string? description)
        {
            _status = newStatus;
            _description = description;
            _lastTransition = DateTimeOffset.UtcNow;
        }
    }
}

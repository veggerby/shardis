namespace Shardis.Migration.Throttling;

/// <summary>Simple hysteresis-based budget governor. NOT thread-safe high contention focused (lightweight sample).</summary>
public sealed class SimpleBudgetGovernor : IBudgetGovernor
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ShardHealth> _latest = new();

    private int _globalBudget;
    private readonly int _initialGlobal;
    private readonly int _minGlobal;
    private readonly int _maxPerShard;

    private readonly List<(string shard, object token)> _inUse = new();

    /// <summary>Gets the current global budget (may fluctuate downward on unhealthy signals and recover gradually).</summary>
    public int CurrentGlobalBudget => Volatile.Read(ref _globalBudget);
    /// <summary>Gets the fixed maximum concurrent operations allowed per shard.</summary>
    public int MaxPerShardBudget => _maxPerShard;

    /// <summary>Creates a new <see cref="SimpleBudgetGovernor"/>.</summary>
    /// <param name="initialGlobal">Starting global concurrency budget.</param>
    /// <param name="minGlobal">Lower bound for global budget under sustained unhealthy signals.</param>
    /// <param name="maxPerShard">Per-shard concurrency cap.</param>
    public SimpleBudgetGovernor(int initialGlobal = 256, int minGlobal = 32, int maxPerShard = 16)
    {
        _initialGlobal = initialGlobal;
        _globalBudget = initialGlobal;
        _minGlobal = minGlobal;
        _maxPerShard = maxPerShard;
    }

    /// <inheritdoc />
    public void Report(ShardHealth health)
    {
        lock (_lock)
        {
            _latest[health.ShardId] = health;
        }
    }

    /// <inheritdoc />
    public void Recalculate()
    {
        lock (_lock)
        {
            var unhealthy = _latest.Values.Any(h => h.P95LatencyMs > 500 || h.MismatchRate > 0.5);

            if (unhealthy)
            {
                _globalBudget = Math.Max(_minGlobal, _globalBudget - Math.Max(8, _globalBudget / 4));
            }
            else if (_globalBudget < _initialGlobal)
            {
                // Gradual recovery
                _globalBudget = Math.Min(_initialGlobal, _globalBudget + Math.Max(4, _globalBudget / 10));
            }
        }
    }

    /// <inheritdoc />
    public bool TryAcquire(out object token, string shardId)
    {
        lock (_lock)
        {
            if (_inUse.Count >= _globalBudget)
            {
                token = null!; return false;
            }

            var shardInUse = _inUse.Count(t => t.shard == shardId);
            if (shardInUse >= _maxPerShard)
            {
                token = null!; return false;
            }

            token = new object();
            _inUse.Add((shardId, token));
            return true;
        }
    }

    /// <inheritdoc />
    public void Release(object token, string shardId)
    {
        lock (_lock)
        {
            for (int i = 0; i < _inUse.Count; i++)
            {
                if (ReferenceEquals(_inUse[i].token, token))
                {
                    _inUse.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
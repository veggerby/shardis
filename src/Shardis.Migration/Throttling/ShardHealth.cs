namespace Shardis.Migration.Throttling;

/// <summary>Represents health metrics for a shard used to tune migration concurrency.</summary>
/// <param name="ShardId">Shard identifier string.</param>
/// <param name="P95LatencyMs">Recent p95 latency in milliseconds for critical operations.</param>
/// <param name="MismatchRate">Recent verification mismatch rate (0..1).</param>
public readonly record struct ShardHealth(string ShardId, double P95LatencyMs, double MismatchRate);

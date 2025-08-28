namespace Shardis.Testing;

/// <summary>Represents skew intensity for deterministic delay scheduling.</summary>
public enum Skew
{
    /// <summary>No artificial skew; all shards share identical scale.</summary>
    None,
    /// <summary>Mild skew (max/min ratio ~=3x) for moderate imbalance scenarios.</summary>
    Mild,
    /// <summary>Harsh skew (max/min ratio ~=10x) stressing fairness and streaming behavior.</summary>
    Harsh
}
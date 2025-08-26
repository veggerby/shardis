namespace Shardis.Migration.Model;

/// <summary>
/// Represents the state of an individual key movement within a migration plan.
/// </summary>
public enum KeyMoveState
{
    /// <summary>The key move is planned but work has not yet started.</summary>
    Planned,
    /// <summary>The key data is currently being copied.</summary>
    Copying,
    /// <summary>The key data has been copied to the target shard.</summary>
    Copied,
    /// <summary>The copied data is being verified for correctness.</summary>
    Verifying,
    /// <summary>The copied data has been verified and is ready for swap.</summary>
    Verified,
    /// <summary>The key is in the process of being swapped to the new shard.</summary>
    Swapping,
    /// <summary>The migration for this key completed successfully.</summary>
    Done,
    /// <summary>The migration for this key failed permanently.</summary>
    Failed
}
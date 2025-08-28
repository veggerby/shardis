namespace Shardis.Query;

internal sealed class BasicQueryCapabilities(bool ordering, bool pagination) : IShardQueryCapabilities
{
    public bool SupportsOrdering { get; } = ordering;
    public bool SupportsPagination { get; } = pagination;
    public static readonly IShardQueryCapabilities None = new BasicQueryCapabilities(false, false);
}
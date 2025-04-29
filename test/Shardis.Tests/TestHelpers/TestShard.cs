using Shardis.Model;

namespace Shardis.Tests.TestHelpers;

public class TestShard<TSession> : IShard<TSession>
{
    public ShardId ShardId { get; }
    private readonly TSession _session;

    public TestShard(string id, TSession session)
    {
        ShardId = new ShardId(id);
        _session = session;
    }

    public TSession CreateSession() => _session;
}
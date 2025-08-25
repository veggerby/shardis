using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;

namespace Shardis.Tests;

public class ShardQueryAnyFirstTests
{
    private sealed class DummyQueryableShard(string id, IEnumerable<int> data) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        private readonly IEnumerable<int> _data = data;
        private readonly IShardQueryExecutor<string> _executor = Substitute.For<IShardQueryExecutor<string>>();

        public string CreateSession() => string.Empty;
        public IShardQueryExecutor<string> QueryExecutor => _executor;
    }

    private static IShardStreamBroadcaster<string> CreateBroadcaster(params (string id, IEnumerable<int> data)[] shards)
    {
        var shardObjs = shards.Select(s => (IShard<string>)new DummyQueryableShard(s.id, s.data)).ToList();
        return new ShardStreamBroadcaster<IShard<string>, string>(shardObjs);
    }

    [Fact]
    public async Task AnyAsync_ShouldReturnTrue_WhenAnyShardHasData()
    {
        // arrange
        var broadcaster = CreateBroadcaster(("s1", Enumerable.Empty<int>()), ("s2", new[] { 1, 2 }));
        var queryable = new ShardQuery<string, int>(broadcaster, _ => new List<int> { 1, 2 }.AsQueryable());

        // act
        var result = await queryable.AnyAsync();

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FirstAsync_ShouldThrow_WhenNoElements()
    {
        // arrange
        var broadcaster = CreateBroadcaster(("s1", Enumerable.Empty<int>()));
        var queryable = new ShardQuery<string, int>(broadcaster, _ => Enumerable.Empty<int>().AsQueryable());

        // act
        Func<Task> act = async () => await queryable.FirstAsync();

        // assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
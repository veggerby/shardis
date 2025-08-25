using Shardis.Hashing;
using Shardis.Model;

namespace Shardis.Tests;

public class DefaultShardKeyHasherTests
{
    [Fact]
    public void Instance_Should_Return_StringHasher_For_String()
    {
        // act
        var hasher = DefaultShardKeyHasher<string>.Instance;

        // assert
        hasher.Should().BeSameAs(StringShardKeyHasher.Instance);
    }

    [Fact]
    public void Instance_Should_Return_Int32Hasher_For_Int()
    {
        // act
        var hasher = DefaultShardKeyHasher<int>.Instance;

        // assert
        hasher.Should().BeSameAs(Int32ShardKeyHasher.Instance);
    }

    private readonly record struct Dummy(int Value);

    [Fact]
    public void Instance_Should_Throw_For_Unsupported_Type()
    {
        // act
        Action act = () => _ = DefaultShardKeyHasher<Dummy>.Instance;

        // assert
        act.Should().Throw<ShardisException>();
    }
}
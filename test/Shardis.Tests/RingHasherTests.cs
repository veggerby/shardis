using AwesomeAssertions;

using Shardis.Hashing;

namespace Shardis.Tests;

public class RingHasherTests
{
    [Fact]
    public void Fnv1aHasher_Should_Produce_Different_Value_Than_Default_For_Typical_Input()
    {
        // arrange
        var input = "tenant-123";
        // act
        var fnv = Fnv1aShardRingHasher.Instance.Hash(input);
        var def = DefaultShardRingHasher.Instance.Hash(input);
        // assert
        fnv.ShouldNotEqual(def);
    }

    [Fact]
    public void Fnv1aHasher_Should_Be_Deterministic()
    {
        // arrange
        var input = "abcXYZ";
        // act
        var h1 = Fnv1aShardRingHasher.Instance.Hash(input);
        var h2 = Fnv1aShardRingHasher.Instance.Hash(input);
        // assert
        h1.ShouldEqual(h2);
    }
}
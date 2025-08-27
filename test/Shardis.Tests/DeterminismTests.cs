using Shardis.Testing;

namespace Shardis.Tests;

public class DeterminismTests
{
    [Fact]
    public void MakeDelays_SameSeed_ProducesIdenticalSchedules()
    {
        // arrange
        var d1 = Determinism.Create(1337);
        var d2 = Determinism.Create(1337);

        // act
        var s1 = d1.MakeDelays(3, Skew.Mild, TimeSpan.FromMilliseconds(5), steps: 8);
        var s2 = d2.MakeDelays(3, Skew.Mild, TimeSpan.FromMilliseconds(5), steps: 8);

        // assert
        for (int shard = 0; shard < 3; shard++)
        {
            s1[shard].Should().HaveCount(8);
            s1[shard].Zip(s2[shard]).All(p => p.First == p.Second).Should().BeTrue();
        }
    }

    [Fact]
    public void MakeDelays_DifferentSeeds_ProduceDifferentSchedules()
    {
        // arrange (include jitter so seed affects schedule)

        // act
        var s1 = Determinism.Create(100).MakeDelays(2, Skew.Mild, TimeSpan.FromMilliseconds(5), steps: 16, jitter: 0.2);
        var s2 = Determinism.Create(101).MakeDelays(2, Skew.Mild, TimeSpan.FromMilliseconds(5), steps: 16, jitter: 0.2);

        // assert (at least one entry differs)
        bool anyDiff = false;
        for (int shard = 0; shard < 2; shard++)
        {
            for (int i = 0; i < 16; i++)
            {
                if (s1[shard][i] != s2[shard][i]) { anyDiff = true; break; }
            }
        }
        anyDiff.Should().BeTrue();
    }

    [Fact]
    public void SkewProfiles_ShowIncreasingMaxDelay()
    {
        // arrange

        // act
        var mild = Determinism.Create(42).MakeDelays(4, Skew.Mild, TimeSpan.FromMilliseconds(2), steps: 4);
        var harsh = Determinism.Create(42).MakeDelays(4, Skew.Harsh, TimeSpan.FromMilliseconds(2), steps: 4);
        double mildMax = mild.SelectMany(x => x).Max(ts => ts.TotalMilliseconds);
        double harshMax = harsh.SelectMany(x => x).Max(ts => ts.TotalMilliseconds);

        // assert
        harshMax.Should().BeGreaterThan(mildMax);
    }
}
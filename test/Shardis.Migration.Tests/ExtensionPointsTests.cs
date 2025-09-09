using Shardis.Migration.Abstractions;
using Shardis.Migration.Throttling;

namespace Shardis.Migration.Tests;

public class ExtensionPointsTests
{
    [Fact]
    public void Fnv1a64Hasher_Stable_For_Same_Input()
    {
        // arrange
        var hasher = new Fnv1a64Hasher();
        var data = System.Text.Encoding.UTF8.GetBytes("hello world");

        // act
        var h1 = hasher.Hash(data);
        var h2 = hasher.Hash(data);

        // assert
        h1.Should().Be(h2);
    }

    [Fact]
    public void JsonCanonicalizer_Produces_Deterministic_Output()
    {
        // arrange
        var canon = new JsonStableCanonicalizer();
        var obj1 = new { B = 2, A = 1 }; // anonymous type property order stable by compiler
        var obj2 = new { B = 2, A = 1 };

        // act
        var b1 = canon.ToCanonicalUtf8(obj1);
        var b2 = canon.ToCanonicalUtf8(obj2);

        // assert
        b1.SequenceEqual(b2).Should().BeTrue();
    }

    private sealed class Sample { public int Value { get; set; } }

    [Fact]
    public void NoOpProjection_Returns_Same_Instance_When_Types_Match()
    {
        // arrange
        var proj = NoOpEntityProjectionStrategy.Instance;
        var sample = new Sample { Value = 42 };

        // act
        var result = proj.Project<Sample, Sample>(sample, new ProjectionContext(null, null));

        // assert
        result.Should().BeSameAs(sample);
    }

    [Fact]
    public void NoOpProjection_Different_Types_Throws()
    {
        // arrange
        var proj = NoOpEntityProjectionStrategy.Instance;

        // act
        Action act = () => proj.Project<Sample, string>(new Sample(), new ProjectionContext(null, null));

        // assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SimpleBudgetGovernor_Budget_Decreases_On_Unhealthy_And_Recovers()
    {
        // arrange
        var gov = new SimpleBudgetGovernor(initialGlobal: 64, minGlobal: 8, maxPerShard: 4);
        var initial = gov.CurrentGlobalBudget;

        // act
        gov.Report(new ShardHealth("s1", P95LatencyMs: 800, MismatchRate: 0)); // unhealthy
        gov.Recalculate();
        var afterDrop = gov.CurrentGlobalBudget;

        // recover path
        gov.Report(new ShardHealth("s1", P95LatencyMs: 50, MismatchRate: 0));
        for (int i = 0; i < 10; i++) { gov.Recalculate(); }
        var afterRecover = gov.CurrentGlobalBudget;

        // assert
        afterDrop.Should().BeLessThan(initial);
        afterRecover.Should().BeGreaterThan(afterDrop);
        afterRecover.Should().BeLessThanOrEqualTo(initial);
    }

    [Fact]
    public void SimpleBudgetGovernor_Acquire_And_Release_Works()
    {
        // arrange
        var gov = new SimpleBudgetGovernor(initialGlobal: 4, minGlobal: 2, maxPerShard: 2);

        // act
        var acquired = new List<object>();
        for (int i = 0; i < 2; i++)
        {
            if (gov.TryAcquire(out var token, "shardA"))
            {
                acquired.Add(token);
            }
        }
        var third = gov.TryAcquire(out var token3, "shardA"); // should hit per-shard cap
        gov.Release(acquired[0], "shardA");
        var afterRelease = gov.TryAcquire(out var token4, "shardA");

        // assert
        acquired.Count.Should().Be(2);
        third.Should().BeFalse();
        afterRelease.Should().BeTrue();
    }
}
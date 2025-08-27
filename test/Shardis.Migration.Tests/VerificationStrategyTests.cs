using Shardis.Migration.Abstractions;
using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class VerificationStrategyTests
{
    private static KeyMove<string> Move() => new(new("k1"), new("s1"), new("s2"));

    [Fact]
    public async Task FullEquality_Delegates_To_Mover()
    {
        var mover = new InMemoryDataMover<string>();
        var strat = new FullEqualityVerificationStrategy<string>(mover);
        var move = Move();
        await mover.CopyAsync(move, CancellationToken.None);
        var ok = await strat.VerifyAsync(move, CancellationToken.None);
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task HashOnly_Returns_False_When_Mismatch_Injected()
    {
        var strat = new HashOnlyVerificationStrategy<string> { MismatchInjector = _ => true };
        var ok = await strat.VerifyAsync(Move(), CancellationToken.None);
        ok.Should().BeFalse();
    }
}
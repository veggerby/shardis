using Shardis.Migration.InMemory;
using Shardis.Migration.Model;

namespace Shardis.Migration.Tests;

public class VerificationStrategyTests
{
    private static KeyMove<string> Move() => new(new("k1"), new("s1"), new("s2"));

    [Fact]
    public async Task FullEquality_Delegates_To_Mover()
    {
        // arrange
        var mover = new InMemoryDataMover<string>();
        var strat = new FullEqualityVerificationStrategy<string>(mover);
        var move = Move();
        await mover.CopyAsync(move, CancellationToken.None);

        // act
        var ok = await strat.VerifyAsync(move, CancellationToken.None);

        // assert
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task HashOnly_Returns_False_When_Mismatch_Injected()
    {
        // arrange
        var strat = new HashOnlyVerificationStrategy<string> { MismatchInjector = _ => true };

        // act
        var ok = await strat.VerifyAsync(Move(), CancellationToken.None);

        // assert
        ok.Should().BeFalse();
    }
}
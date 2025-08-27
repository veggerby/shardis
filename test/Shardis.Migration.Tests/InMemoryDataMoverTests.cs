using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class InMemoryDataMoverTests
{
    private static KeyMove<string> Move() => new(new ShardKey<string>("k1"), new("s1"), new("s2"));

    [Fact]
    public async Task Copy_Then_Verify_Succeeds()
    {
        // arrange
        var mover = new InMemoryDataMover<string>();
        var move = Move();

        // act
        await mover.CopyAsync(move, CancellationToken.None);
        var ok = await mover.VerifyAsync(move, CancellationToken.None);

        // assert
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task Copy_Failure_Injector_Throws()
    {
        // arrange
        var mover = new InMemoryDataMover<string> { CopyFailureInjector = _ => new InvalidOperationException("x") };

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => mover.CopyAsync(Move(), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_Mismatch_Returns_False()
    {
        // arrange
        var mover = new InMemoryDataMover<string>();
        var move = Move();
        await mover.CopyAsync(move, CancellationToken.None);
        mover.VerificationMismatchInjector = _ => true;

        // act
        var ok = await mover.VerifyAsync(move, CancellationToken.None);

        // assert
        ok.Should().BeFalse();
    }
}
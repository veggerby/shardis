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
        var mover = new InMemoryDataMover<string>();
        var move = Move();
        await mover.CopyAsync(move, CancellationToken.None);
        var ok = await mover.VerifyAsync(move, CancellationToken.None);
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task Copy_Failure_Injector_Throws()
    {
        var mover = new InMemoryDataMover<string> { CopyFailureInjector = _ => new InvalidOperationException("x") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => mover.CopyAsync(Move(), CancellationToken.None));
    }

    [Fact]
    public async Task Verify_Mismatch_Returns_False()
    {
        var mover = new InMemoryDataMover<string>();
        var move = Move();
        await mover.CopyAsync(move, CancellationToken.None);
        mover.VerificationMismatchInjector = _ => true;
        var ok = await mover.VerifyAsync(move, CancellationToken.None);
        ok.Should().BeFalse();
    }
}
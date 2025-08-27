using Shardis.Migration.Execution;

namespace Shardis.Migration.Tests;

public class ShardMigrationOptionsTests
{
    [Fact]
    public void Defaults_Are_Within_Valid_Range()
    {
        // arrange
        var opt = new ShardMigrationOptions();

        // act & assert
        opt.CopyConcurrency.Should().BeGreaterThan(0);
        opt.VerifyConcurrency.Should().BeGreaterThan(0);
        opt.SwapBatchSize.Should().BeGreaterThan(0);
        opt.MaxRetries.Should().BeGreaterThan(-1);
        opt.RetryBaseDelay.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CopyConcurrency_Invalid_Throws(int value)
    {
        // arrange
        Action act = () => _ = new ShardMigrationOptions { CopyConcurrency = value };

        // act & assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Boundary_Max_Retry_Accepts_Zero()
    {
        // arrange
        var opt = new ShardMigrationOptions { MaxRetries = 0 };

        // act & assert
        opt.MaxRetries.Should().Be(0);
    }
}
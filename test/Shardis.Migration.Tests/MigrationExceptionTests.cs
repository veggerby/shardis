using Shardis.Migration.Exceptions;
using Shardis.Model;

namespace Shardis.Migration.Tests;

public class MigrationExceptionTests
{
    [Fact]
    public void ShardMigrationException_ShouldIncludeMigrationContext()
    {
        // arrange
        var sourceShardId = new ShardId("source-shard");
        var targetShardId = new ShardId("target-shard");
        var phase = "Copy";
        var planId = "plan-123";
        int attemptCount = 2;

        // act
        var exception = new ShardMigrationException(
            "Migration failed",
            null,
            phase,
            sourceShardId,
            targetShardId,
            attemptCount,
            planId,
            null);

        // assert
        exception.Message.Should().Be("Migration failed");
        exception.Phase.Should().Be(phase);
        exception.SourceShardId.Should().Be(sourceShardId);
        exception.TargetShardId.Should().Be(targetShardId);
        exception.AttemptCount.Should().Be(attemptCount);
        exception.PlanId.Should().Be(planId);
        exception.DiagnosticContext["Phase"].Should().Be("Copy");
        exception.DiagnosticContext["SourceShardId"].Should().Be("source-shard");
        exception.DiagnosticContext["TargetShardId"].Should().Be("target-shard");
        exception.DiagnosticContext["AttemptCount"].Should().Be(2);
        exception.DiagnosticContext["PlanId"].Should().Be("plan-123");
    }

    [Fact]
    public void ShardMigrationException_WithMinimalContext_ShouldWork()
    {
        // arrange & act
        var exception = new ShardMigrationException("Migration failed");

        // assert
        exception.Message.Should().Be("Migration failed");
        exception.Phase.Should().BeNull();
        exception.SourceShardId.Should().BeNull();
        exception.TargetShardId.Should().BeNull();
        exception.AttemptCount.Should().BeNull();
        exception.PlanId.Should().BeNull();
        exception.DiagnosticContext.Should().BeEmpty();
    }

    [Fact]
    public void ShardMigrationException_WithInnerException_ShouldPreserveIt()
    {
        // arrange
        var innerException = new Exception("Inner exception");

        // act
        var exception = new ShardMigrationException("Migration failed", innerException);

        // assert
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void ShardMigrationException_WithAdditionalContext_ShouldMergeAllContext()
    {
        // arrange
        var additionalContext = new Dictionary<string, object?>
        {
            ["CustomKey"] = "CustomValue",
            ["KeysProcessed"] = 100
        };

        // act
        var exception = new ShardMigrationException(
            "Migration failed",
            null,
            "Verify",
            null,
            null,
            null,
            null,
            additionalContext);

        // assert
        exception.DiagnosticContext["Phase"].Should().Be("Verify");
        exception.DiagnosticContext["CustomKey"].Should().Be("CustomValue");
        exception.DiagnosticContext["KeysProcessed"].Should().Be(100);
        exception.DiagnosticContext.Should().HaveCount(3);
    }
}

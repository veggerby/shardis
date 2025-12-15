using Shardis.Model;

namespace Shardis.Tests;

public class ExceptionContractTests
{
    [Fact]
    public void ShardisException_ShouldIncludeDiagnosticContext()
    {
        // arrange
        var context = new Dictionary<string, object?>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 42
        };

        // act
        var exception = new ShardisException("Test message", null, context);

        // assert
        exception.Message.Should().Be("Test message");
        exception.DiagnosticContext.Should().NotBeNull();
        exception.DiagnosticContext.Should().HaveCount(2);
        exception.DiagnosticContext["Key1"].Should().Be("Value1");
        exception.DiagnosticContext["Key2"].Should().Be(42);
    }

    [Fact]
    public void ShardisException_WithNullContext_ShouldHaveEmptyDiagnosticContext()
    {
        // arrange & act
        var exception = new ShardisException("Test message");

        // assert
        exception.DiagnosticContext.Should().NotBeNull();
        exception.DiagnosticContext.Should().BeEmpty();
    }

    [Fact]
    public void ShardRoutingException_ShouldIncludeRoutingContext()
    {
        // arrange
        var shardId = new ShardId("shard-1");
        uint keyHash = 0x12345678;
        int shardCount = 5;

        // act
        var exception = new ShardRoutingException(
            "Routing failed",
            null,
            shardId,
            keyHash,
            shardCount,
            null);

        // assert
        exception.Message.Should().Be("Routing failed");
        exception.ShardId.Should().Be(shardId);
        exception.KeyHash.Should().Be(keyHash);
        exception.ShardCount.Should().Be(shardCount);
        exception.DiagnosticContext["ShardId"].Should().Be("shard-1");
        exception.DiagnosticContext["KeyHash"].Should().Be("12345678");
        exception.DiagnosticContext["ShardCount"].Should().Be(5);
    }

    [Fact]
    public void ShardStoreException_ShouldIncludeStoreContext()
    {
        // arrange
        var shardId = new ShardId("shard-2");
        var operation = "TryAssign";
        int attemptCount = 3;

        // act
        var exception = new ShardStoreException(
            "Store operation failed",
            null,
            operation,
            shardId,
            attemptCount,
            null);

        // assert
        exception.Message.Should().Be("Store operation failed");
        exception.Operation.Should().Be(operation);
        exception.ShardId.Should().Be(shardId);
        exception.AttemptCount.Should().Be(attemptCount);
        exception.DiagnosticContext["Operation"].Should().Be("TryAssign");
        exception.DiagnosticContext["ShardId"].Should().Be("shard-2");
        exception.DiagnosticContext["AttemptCount"].Should().Be(3);
    }

    [Fact]
    public void ShardQueryException_ShouldIncludeQueryContext()
    {
        // arrange
        var shardId = new ShardId("shard-3");
        var phase = "Execution";
        int targetedShardCount = 4;

        // act
        var exception = new ShardQueryException(
            "Query failed",
            null,
            phase,
            shardId,
            targetedShardCount,
            null);

        // assert
        exception.Message.Should().Be("Query failed");
        exception.Phase.Should().Be(phase);
        exception.ShardId.Should().Be(shardId);
        exception.TargetedShardCount.Should().Be(targetedShardCount);
        exception.DiagnosticContext["Phase"].Should().Be("Execution");
        exception.DiagnosticContext["ShardId"].Should().Be("shard-3");
        exception.DiagnosticContext["TargetedShardCount"].Should().Be(4);
    }

    [Fact]
    public void ShardTopologyException_ShouldIncludeTopologyContext()
    {
        // arrange
        long topologyVersion = 123;
        int keyCount = 1000;
        int maxKeyCount = 500;

        // act
        var exception = new ShardTopologyException(
            "Topology validation failed",
            null,
            topologyVersion,
            keyCount,
            maxKeyCount,
            null);

        // assert
        exception.Message.Should().Be("Topology validation failed");
        exception.TopologyVersion.Should().Be(topologyVersion);
        exception.KeyCount.Should().Be(keyCount);
        exception.MaxKeyCount.Should().Be(maxKeyCount);
        exception.DiagnosticContext["TopologyVersion"].Should().Be(123L);
        exception.DiagnosticContext["KeyCount"].Should().Be(1000);
        exception.DiagnosticContext["MaxKeyCount"].Should().Be(500);
    }

    [Fact]
    public void ShardRoutingException_WithAdditionalContext_ShouldMergeAllContext()
    {
        // arrange
        var shardId = new ShardId("shard-4");
        var additionalContext = new Dictionary<string, object?>
        {
            ["CustomKey"] = "CustomValue"
        };

        // act
        var exception = new ShardRoutingException(
            "Routing failed",
            null,
            shardId,
            null,
            null,
            additionalContext);

        // assert
        exception.DiagnosticContext["ShardId"].Should().Be("shard-4");
        exception.DiagnosticContext["CustomKey"].Should().Be("CustomValue");
        exception.DiagnosticContext.Should().HaveCount(2);
    }

    [Fact]
    public void AllExceptions_ShouldSupportInnerException()
    {
        // arrange
        var innerException = new Exception("Inner exception");

        // act
        var routingEx = new ShardRoutingException("Routing failed", innerException);
        var storeEx = new ShardStoreException("Store failed", innerException);
        var queryEx = new ShardQueryException("Query failed", innerException);
        var topologyEx = new ShardTopologyException("Topology failed", innerException);

        // assert
        routingEx.InnerException.Should().Be(innerException);
        storeEx.InnerException.Should().Be(innerException);
        queryEx.InnerException.Should().Be(innerException);
        topologyEx.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void DiagnosticContext_ShouldBeReadOnly()
    {
        // arrange
        var context = new Dictionary<string, object?> { ["Key"] = "Value" };
        var exception = new ShardisException("Test", null, context);

        // act & assert
        var readOnlyDict = exception.DiagnosticContext as System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>;
        readOnlyDict.Should().NotBeNull("DiagnosticContext should be ReadOnlyDictionary");
    }

    [Fact]
    public void DiagnosticContext_ShouldBeIsolatedFromOriginalDictionary()
    {
        // arrange
        var context = new Dictionary<string, object?> { ["Key"] = "Value" };
        var exception = new ShardisException("Test", null, context);

        // act
        context["Key"] = "Modified";
        context.Add("NewKey", "NewValue");

        // assert
        exception.DiagnosticContext["Key"].Should().Be("Value");
        exception.DiagnosticContext.Should().NotContainKey("NewKey");
    }
}

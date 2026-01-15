# Integration Testing with Testcontainers

This guide explains how to run and write integration tests in the Shardis project using Testcontainers.

## Overview

Shardis uses [Testcontainers for .NET](https://dotnet.testcontainers.org/) to provide deterministic, isolated, and reproducible integration test environments. Testcontainers automatically manages Docker containers for external dependencies like Redis and PostgreSQL, eliminating the need for manual container setup.

## Running Integration Tests

### Prerequisites

- Docker must be installed and running on your system
- .NET 9.0 or later SDK
- No manual container setup required!

### Running All Integration Tests

```bash
# From repository root
dotnet test --filter "Category=Integration"
```

### Running Integration Tests for Specific Packages

```bash
# Redis integration tests (Shardis.Redis)
dotnet test test/Shardis.Tests/Shardis.Tests.csproj --filter "Category=Integration"

# Marten integration tests
dotnet test test/Shardis.Marten.Tests/Shardis.Marten.Tests.csproj --filter "Category=Integration"

# Migration integration tests (Marten-based)
dotnet test test/Shardis.Migration.Marten.Tests/Shardis.Migration.Marten.Tests.csproj --filter "Category=Integration"
```

### Running Unit Tests Only (Fast)

```bash
# Exclude integration tests
dotnet test --filter "Category!=Integration"
```

## How It Works

### Testcontainers Lifecycle

1. **Test Class Setup**: When a test class with `IClassFixture<FixtureName>` is instantiated, the fixture's `InitializeAsync()` method starts the required container(s)
2. **Container Startup**: Testcontainers pulls the Docker image (if not cached) and starts the container
3. **Test Execution**: Tests run with the containerized service available via the fixture's connection string
4. **Container Cleanup**: After all tests in the class complete, the fixture's `DisposeAsync()` method stops and removes the container

### Automatic Port Assignment

Testcontainers automatically assigns random available ports to avoid conflicts:
- Multiple test runs can execute in parallel
- No manual port management required
- No conflicts with existing services on standard ports (5432, 6379, etc.)

## Writing Integration Tests

### Redis Integration Tests

```csharp
using Shardis.Model;
using Shardis.Redis;
using Xunit;

namespace Shardis.Tests;

[Trait("Category", "Integration")]
public sealed class MyRedisIntegrationTests : IClassFixture<RedisContainerFixture>
{
    private readonly RedisContainerFixture _fixture;

    public MyRedisIntegrationTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        // arrange
        var store = new RedisShardMapStore<string>(_fixture.ConnectionString);
        
        // act
        // ... your test code ...
        
        // assert
        // ... assertions ...
    }
}
```

### PostgreSQL/Marten Integration Tests

```csharp
using Marten;
using Shardis.Model;
using Xunit;

namespace Shardis.Marten.Tests;

[Trait("Category", "Integration")]
public sealed class MyMartenIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public MyMartenIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact] // Use PostgresFact attribute for Marten tests
    public async Task MyTest()
    {
        // arrange
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_fixture.ConnectionString);
        });
        var shard = new MartenShard(new ShardId("test"), store);
        
        // act
        // ... your test code ...
        
        // assert
        // ... assertions ...
    }
}
```

## Container Fixtures

### RedisContainerFixture

**Location**: `test/Shardis.Tests/RedisContainerFixture.cs`

**Container**: `redis:7-alpine`

**Usage**: For testing `RedisShardMapStore<TKey>` and other Redis-dependent components

**Properties**:
- `ConnectionString`: Redis connection string (format: `localhost:PORT`)

### PostgresContainerFixture

**Locations**: 
- `test/Shardis.Marten.Tests/PostgresContainerFixture.cs`
- `test/Shardis.Migration.Marten.Tests/PostgresContainerFixture.cs`

**Container**: `postgres:15-alpine`

**Usage**: For testing Marten query executors, document stores, and migration operations

**Properties**:
- `ConnectionString`: PostgreSQL connection string
- `Store`: Pre-configured Marten `DocumentStore` instance (Marten.Tests fixture only)

## Test Categories

All integration tests must be tagged with `[Trait("Category", "Integration")]` at the class level:

```csharp
[Trait("Category", "Integration")]
public sealed class MyIntegrationTests : IClassFixture<SomeContainerFixture>
{
    // tests...
}
```

This allows:
- Running only integration tests: `dotnet test --filter "Category=Integration"`
- Excluding integration tests: `dotnet test --filter "Category!=Integration"`
- CI/CD pipeline filtering

## CI/CD Integration

### GitHub Actions

Integration tests run automatically in CI via Testcontainers:

**Workflow**: `.github/workflows/integration-tests.yml`

Key points:
- Docker is available in GitHub Actions runners
- No manual service containers needed
- Testcontainers manages all container lifecycle
- Tests run in parallel when safe

**Workflow**: `.github/workflows/integration-marten.yml`

Updated to use Testcontainers instead of GitHub Actions service containers.

## Troubleshooting

### Docker Not Available

**Error**: "Docker is not running"

**Solution**: Ensure Docker Desktop (or Docker Engine) is running on your system

### Port Conflicts

**Error**: "Port already in use"

**Solution**: This should not happen with Testcontainers (it assigns random ports). If it does, ensure no other test run is active.

### Container Startup Timeout

**Error**: "Container failed to start within timeout"

**Solution**: 
- Check Docker daemon is healthy
- Increase timeout if on slow CI runners (Testcontainers uses reasonable defaults)
- Check Docker image is pullable (network access)

### Image Pull Failures

**Error**: "Failed to pull image"

**Solution**:
- Ensure internet connectivity
- Check Docker registry access
- Images used: `redis:7-alpine`, `postgres:15-alpine`

## Performance Considerations

### Container Reuse

Currently, fixtures create a new container per test class. This provides isolation but adds startup overhead.

**Future optimization**: Share containers across test classes when deterministic cleanup is guaranteed.

### Image Caching

Docker caches pulled images locally. First run downloads images; subsequent runs are faster.

### Parallel Execution

xUnit runs test classes in parallel by default. Testcontainers' random port assignment ensures no conflicts.

## Best Practices

1. **Use fixtures**: Always use `IClassFixture<T>` for integration tests requiring containers
2. **Tag tests**: Always add `[Trait("Category", "Integration")]` to integration test classes
3. **Deterministic data**: Use unique prefixes/GUIDs in test data to avoid interference
4. **Cleanup**: Trust Testcontainers to clean up containers; don't leave manual cleanup code
5. **Connection strings**: Always use `_fixture.ConnectionString` from the fixture, never hardcode
6. **Minimal assertions**: Integration tests should verify integration, not business logic details

## Examples

See existing integration tests:
- `test/Shardis.Tests/RedisShardMapStoreIntegrationTests.cs` - Redis integration
- `test/Shardis.Marten.Tests/MartenQueryExecutorTests.cs` - Marten query execution
- `test/Shardis.Marten.Tests/MartenMetricsAndCancellationTests.cs` - Marten metrics
- `test/Shardis.Migration.Marten.Tests/MartenExecutorIntegrationTests.cs` - Migration execution

## Resources

- [Testcontainers for .NET Documentation](https://dotnet.testcontainers.org/)
- [xUnit Shared Context Documentation](https://xunit.net/docs/shared-context)
- [Shardis Contributing Guide](../../CONTRIBUTING.md)

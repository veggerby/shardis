# AGENTS.md

## Project Overview

**Shardis** is a production-grade .NET sharding framework providing deterministic key-based routing across multiple database shards. Built for event sourcing, CQRS, and multi-tenant architectures, it maintains strict determinism over cleverness and clarity over abstraction.

### Key Technologies

- **.NET 8.0/9.0**: Multi-targeting for compatibility
- **C# 12+**: File-scoped namespaces, primary constructors, record types
- **xUnit**: Testing framework
- **NSubstitute**: Mocking library (never Moq)
- **AwesomeAssertions**: Fluent assertions (never FluentAssertions)
- **BenchmarkDotNet**: Performance benchmarking
- **PostgreSQL**: Primary database (via Marten, Entity Framework Core)
- **Redis**: Distributed shard map storage
- **OpenTelemetry**: Metrics and observability

### Architecture

15 NuGet packages organized by concern:
- **Core**: `Shardis` (routing, models, abstractions)
- **Migration**: `Shardis.Migration` + provider packages (EF Core, Marten, SQL)
- **Query**: `Shardis.Query` + provider packages (EF Core, Marten, InMemory)
- **Infrastructure**: Redis, DI, Logging, Testing packages

## Setup Commands

```bash
# Clone and navigate
git clone https://github.com/veggerby/shardis.git
cd shardis

# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build src/Shardis/Shardis.csproj

# Build in Release mode
dotnet build --configuration Release
```

## Development Workflow

### Quick Start

```bash
# Build and run all tests
dotnet build && dotnet test

# Run specific sample
dotnet run --project samples/SampleApp

# Run migration sample
dotnet run --project samples/Shardis.Migration.Sample
```

### Watch Mode

```bash
# Watch and rebuild on changes (from project directory)
cd src/Shardis
dotnet watch build

# Watch and run tests
cd test/Shardis.Tests
dotnet watch test
```

### Working with Specific Packages

Navigate directly to package directories:
```bash
# Core routing
cd src/Shardis

# Migration
cd src/Shardis.Migration

# Query execution
cd src/Shardis.Query

# Provider-specific
cd src/Shardis.Query.EntityFrameworkCore
cd src/Shardis.Migration.Marten
```

## Testing Instructions

### Run All Tests

```bash
# Full test suite
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"

# With code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Run Specific Test Projects

```bash
# Core tests
dotnet test test/Shardis.Tests/Shardis.Tests.csproj

# Migration tests
dotnet test test/Shardis.Migration.Tests/Shardis.Migration.Tests.csproj

# Query tests
dotnet test test/Shardis.Query.Tests/Shardis.Query.Tests.csproj

# Marten integration tests (requires PostgreSQL)
dotnet test test/Shardis.Marten.Tests/Shardis.Marten.Tests.csproj

# Public API approval tests
dotnet test test/Shardis.PublicApi.Tests/Shardis.PublicApi.Tests.csproj
```

### Test Patterns

```bash
# Run specific test by name pattern
dotnet test --filter "FullyQualifiedName~DefaultShardRouter"

# Run tests in specific namespace
dotnet test --filter "FullyQualifiedName~Shardis.Tests.Routing"

# Run tests by category (if tagged)
dotnet test --filter "Category=Integration"
```

### Test Organization

- **Unit tests**: `test/Shardis.Tests/` - Core routing, hashing, models
- **Migration tests**: `test/Shardis.Migration.Tests/` - Planner, executor, checkpoints
- **Query tests**: `test/Shardis.Query.Tests/` - Broadcasters, merge enumerators, executors
- **Integration tests**: `test/Shardis.Marten.Tests/`, `test/Shardis.Migration.EntityFrameworkCore.Tests/`
- **Public API tests**: `test/Shardis.PublicApi.Tests/` - API surface validation

### Test Requirements

- **All tests** must use `NSubstitute` for mocking (never `Moq`)
- **All tests** must use `AwesomeAssertions` for assertions (never `FluentAssertions`)
- Tests must be divided with `// arrange`, `// act`, `// assert` comments
- Tests must have proper vertical spacing between sections
- Coverage must be maintained for public API surface

## Code Style Guidelines

### Mandatory Style Rules (SHIT-Compliant)

**File Organization:**
- File-scoped namespaces only: `namespace Shardis.Routing;`
- No `#region` folding (ever)
- One type per file (exception: nested private types)

**Formatting:**
- Full curly braces for all control blocks
- Explicit namespaces matching folder structure
- Private fields: `_camelCase`
- Public members: `PascalCase`
- `async/await` everywhere (no `.Result`, `.Wait()`, or sync-over-async)
- Nullable reference types enabled and respected

**Preferred Patterns:**
- `readonly record struct` for immutable value objects
- Primary constructors where useful
- File-scoped namespaces
- Explicit `var` usage for clarity

**Forbidden:**
- Generated banner comments or AI disclaimers
- Console output in library projects (samples only)
- Business logic in models/value types
- Blocking waits (`.Result`, `.Wait()`)
- Hidden side effects or ambient singletons
- Random/non-deterministic behavior in routing

### Code Structure

```csharp
// Good: File-scoped namespace, explicit structure
namespace Shardis.Routing;

public sealed class DefaultShardRouter<TKey, TSession> : IShardRouter<TKey, TSession>
    where TKey : notnull, IEquatable<TKey>
    where TSession : class
{
    private readonly IShardMapStore<TKey> _mapStore;
    
    public DefaultShardRouter(IShardMapStore<TKey> mapStore)
    {
        _mapStore = mapStore;
    }
    
    public async ValueTask<IShard<TSession>> RouteToShardAsync(
        ShardKey<TKey> key,
        CancellationToken ct = default)
    {
        // Implementation
    }
}
```

### Linting and Formatting

```bash
# Check for style violations
dotnet build /warnaserror

# All warnings treated as errors (enforced in Directory.Build.props)
# TreatWarningsAsErrors is enabled globally
```

## Build and Deployment

### Build Commands

```bash
# Debug build (default)
dotnet build

# Release build
dotnet build --configuration Release

# Build specific package
dotnet build src/Shardis/Shardis.csproj --configuration Release

# Clean before build
dotnet clean && dotnet build
```

### Package Creation

```bash
# Create NuGet packages
dotnet pack --configuration Release

# Pack specific project
dotnet pack src/Shardis/Shardis.csproj --configuration Release

# Pack with specific version
dotnet pack --configuration Release /p:Version=1.0.0-preview.1
```

### Versioning

- Version controlled via `GitVersion.yml`
- Semantic versioning enforced
- Preview packages tagged: `1.0.0-preview.1`
- Continuous integration builds set version automatically

## Benchmarks

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run --project benchmarks/Shardis.Benchmarks.csproj --configuration Release

# Run specific benchmark
dotnet run --project benchmarks/Shardis.Benchmarks.csproj --configuration Release --filter *RouterBenchmarks*

# Export results
dotnet run --project benchmarks/Shardis.Benchmarks.csproj --configuration Release --exporters json
```

### Benchmark Categories

- **RouterBenchmarks**: Shard routing performance
- **HasherBenchmarks**: Hash function comparisons
- **MergeEnumeratorBenchmarks**: K-way merge performance
- **QueryBenchmarks**: Query execution latency
- **MigrationThroughputBenchmarks**: Migration executor throughput
- **OrderedMergeBenchmarks**: Ordered streaming merge
- **BroadcasterStreamBenchmarks**: Channel broadcasting

### Performance Standards

- Routing hot path: Minimal allocations (< 100 bytes)
- Query merge latency: Single emission (no buffering)
- Migration throughput: Measured and baselined
- Benchmark results archived in `benchmarks/results/`

## Pull Request Guidelines

### Before Submitting

1. **Build passes**: `dotnet build` (zero warnings)
2. **All tests pass**: `dotnet test` (100% pass rate)
3. **Full solution test**: Run entire test suite, not filtered subsets
4. **Code style**: Follow SHIT-compliant guidelines
5. **Public API**: Update approval tests if API changed
6. **Documentation**: Update README/docs if user-facing change
7. **Benchmarks**: Run if touching critical path (routing, query, merge)

### PR Checklist Template

```markdown
- [ ] Build passes (`dotnet build`)
- [ ] Tests added/updated (`dotnet test`)
- [ ] Full solution test suite executed
- [ ] No style warnings introduced
- [ ] Public APIs documented (XML docs)
- [ ] README/docs updated (if user-facing)
- [ ] Benchmarks unaffected or justified
- [ ] Determinism preserved (no randomness)
- [ ] Thread-safety maintained
```

### Commit Message Format

```
<type>(<scope>): <description>

[optional body]
[optional footer]
```

Examples:
- `feat(routing): add ConsistentHashShardRouter`
- `fix(migration): checkpoint recovery on transient failure`
- `docs(query): update LINQ supported operations`
- `perf(merge): reduce allocations in k-way merge`
- `test(routing): add concurrent routing tests`

## Integration Tests

### PostgreSQL Tests (Marten)

Requires running PostgreSQL instance:

```bash
# Start PostgreSQL (Docker)
docker run --name postgres-test -e POSTGRES_PASSWORD=test -p 5432:5432 -d postgres:15

# Run Marten tests
dotnet test test/Shardis.Marten.Tests/Shardis.Marten.Tests.csproj

# Run migration Marten tests
dotnet test test/Shardis.Migration.Marten.Tests/Shardis.Migration.Marten.Tests.csproj
```

### Redis Tests

Requires running Redis instance:

```bash
# Start Redis (Docker)
docker run --name redis-test -p 6379:6379 -d redis:7-alpine

# Tests automatically detect and use Redis
dotnet test test/Shardis.Tests/Shardis.Tests.csproj
```

## Documentation

### Structure

- **README.md**: User-facing documentation, getting started
- **docs/**: Technical deep-dives, ADRs, API reference
- **CONTRIBUTING.md**: Contribution guidelines
- **CHANGELOG.md**: Version history
- **Package READMEs**: Each package has `src/<Package>/README.md`

### Documentation Updates

When adding features:
1. Update main README.md if user-facing
2. Add/update package README if package-specific
3. Create ADR in `docs/adr/` for architectural decisions
4. Update `docs/index.md` navigation

### Documentation Standards

- Use markdown links: `[file.cs](src/file.cs)` not backticks
- File references: `[Routing](src/Shardis/Routing/DefaultShardRouter.cs#L10)`
- No relative links in package READMEs (breaks on NuGet.org)
- Use full GitHub URLs: `https://github.com/veggerby/shardis/...`

## Project-Specific Context

### Core Principles (from copilot-instructions.md)

1. **Determinism over cleverness**: Routing must be 100% deterministic
2. **Clarity over abstraction bloat**: Keep extensibility points explicit
3. **Safety**: Thread-safe routing & persistence
4. **No leakage**: Sharding concerns never leak into domain models
5. **Stable public surface**: API documented and versioned

### Key Abstractions

- `IShardRouter<TKey, TSession>`: Core routing interface
- `IShardMapStore<TKey>`: Persistence abstraction for mappings
- `IShardKeyHasher<TKey>`: Key hashing strategy
- `IShardRingHasher`: Ring hashing for consistent hash router
- `IShardQueryExecutor`: Query execution pipeline
- `IShardMigrationExecutor<TKey>`: Migration orchestration

### Common Patterns

**Routing (Single-Miss Guarantee):**
```csharp
// Router ensures key assigned exactly once
var shard = await router.RouteToShardAsync(key);
// Subsequent calls return same shard (deterministic)
```

**Query (Streaming-First):**
```csharp
// Prefer streaming over materialization
await foreach (var item in client.Query<Order>().AsAsyncEnumerable())
{
    // Process without buffering
}
```

**Migration (Checkpoint-Based):**
```csharp
// Migrations are resumable via checkpoints
var result = await executor.ExecuteAsync(plan, progress, ct);
if (!result.IsSuccess)
{
    // Resume from last checkpoint
    await executor.ResumeAsync(result.CheckpointId);
}
```

## Troubleshooting

### Common Issues

**Build Warnings:**
- Cause: `TreatWarningsAsErrors=true` enforced globally
- Fix: Address all warnings before proceeding

**Test Failures:**
- Missing dependencies: `dotnet restore`
- Integration tests: Check PostgreSQL/Redis containers running
- Public API changes: Update approval files in `test/PublicApiApproval/`

**Benchmark Errors:**
- Must run in Release: `--configuration Release`
- Ensure no debugger attached
- Close resource-intensive applications

**Package Restore Issues:**
- Clear package cache: `dotnet nuget locals all --clear`
- Restore: `dotnet restore --force`

## Additional Resources

- **GitHub**: https://github.com/veggerby/shardis
- **Issues**: https://github.com/veggerby/shardis/issues
- **Discussions**: https://github.com/veggerby/shardis/discussions
- **License**: MIT (see LICENSE file)

## Quick Reference

### Most Common Commands

```bash
# Full build and test
dotnet build && dotnet test

# Release build with packages
dotnet build -c Release && dotnet pack -c Release

# Run benchmarks
dotnet run --project benchmarks/Shardis.Benchmarks.csproj -c Release

# Run specific sample
dotnet run --project samples/SampleApp

# Integration tests (requires containers)
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=test postgres:15
docker run -d -p 6379:6379 redis:7-alpine
dotnet test test/Shardis.Marten.Tests
```

### File Navigation

```bash
# Core routing
src/Shardis/Routing/

# Migration execution
src/Shardis.Migration/Execution/

# Query orchestration
src/Shardis.Query/

# Tests mirror src structure
test/Shardis.Tests/
test/Shardis.Migration.Tests/
test/Shardis.Query.Tests/
```

# Shardis.Query.InMemory

In-memory query executor for Shardis (testing and prototyping). Deterministic and fast for unit tests and local samples.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Query.InMemory.svg)](https://www.nuget.org/packages/Shardis.Query.InMemory/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Query.InMemory.svg)](https://www.nuget.org/packages/Shardis.Query.InMemory/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Query.InMemory --version 0.1.*
```

## When to use

- Testing, demos, and samples that need deterministic shard-local query behavior.

## What’s included

- `InMemoryQueryExecutor` — deterministic in-memory executor for tests.
- Helpers for seeding deterministic test data.

## Quick start

```csharp
var exec = new InMemoryQueryExecutor(...); // create with seeded test data
await foreach (var item in exec.QueryAsync(...))
{
    // assert test expectations
}
```

## Integration notes

- Intended for tests and examples; pair with `Shardis.Query` core abstractions.

## Samples & tests

- Tests: [Shardis.Query.Tests](https://github.com/veggerby/shardis/tree/main/test/Shardis.Query.Tests)
- Samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)

## Configuration / Options

- No configuration required; the executor is designed for deterministic test inputs. Provide seeded data via constructor helpers.

## Capabilities & limits

- ✅ Very fast and deterministic for unit tests.
- ⚠️ Not intended for production use; lacks paging/backpressure optimizations.

## Versioning & compatibility

- SemVer; see CHANGELOG: [CHANGELOG](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md)

## Contributing

- PRs welcome. See [CONTRIBUTING](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## Links

- NuGet: [Shardis.Query.InMemory on NuGet](https://www.nuget.org/packages/Shardis.Query.InMemory/)
- Repo samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)

## License

- MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>

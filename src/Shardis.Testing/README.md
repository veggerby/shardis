# Shardis.Testing

Testing helpers and in-memory fakes for Shardis: deterministic in-memory implementations, test fixtures, and utilities used by unit tests across the repo.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Testing.svg)](https://www.nuget.org/packages/Shardis.Testing/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Testing.svg)](https://www.nuget.org/packages/Shardis.Testing/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Testing --version 0.1.*
```

## When to use

- Writing unit tests or integration tests that need deterministic, in-memory implementations of Shardis abstractions.

## What’s included

- In-memory `IShardMapStore<T>` and `InMemoryCheckpointStore` reference implementations.
- Test helpers, seeded data factories, and deterministic RNG seeds for reproducible tests.

## Quick start

```csharp
// use in tests to register deterministic in-memory services
services.AddSingleton<IShardMapStore<string>>(new InMemoryShardMapStore<string>());
```

## Samples & tests

- Tests are located in the main test tree: [tests](https://github.com/veggerby/shardis/tree/main/test)

## Capabilities & limits

- ✅ Deterministic in-memory implementations suitable for unit tests.
- ⚠️ Not for production use; intended for test harnesses and fast CI runs.

## Versioning & compatibility

- SemVer; see CHANGELOG: [CHANGELOG](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md)

## Contributing

- PRs welcome. See [CONTRIBUTING](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## Links

- NuGet: [Shardis.Testing on NuGet](https://www.nuget.org/packages/Shardis.Testing/)
- License: [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)

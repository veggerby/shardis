# Shardis.Logging.Console

Minimal console logger for the Shardis logging abstraction.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Logging.Console.svg)](https://www.nuget.org/packages/Shardis.Logging.Console/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Logging.Console.svg)](https://www.nuget.org/packages/Shardis.Logging.Console/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Install

```bash
dotnet add package Shardis.Logging.Console --version 0.1.*
```

## When to use

* Quick local diagnostics while exploring Shardis
* Sample code and tests needing human-readable output
* Temporary troubleshooting in non-production contexts

## What’s included

* `ConsoleShardisLogger` — timestamped console implementation of `IShardisLogger`

## Quick start

```csharp
var logger = new Shardis.Logging.Console.ConsoleShardisLogger(Shardis.Logging.ShardisLogLevel.Debug);
logger.Log(Shardis.Logging.ShardisLogLevel.Information, "Migration started");
```

## Configuration / Options

Pass a minimum level in the constructor; defaults to `Information`.

## Integration notes

Add instance manually where Shardis components accept `IShardisLogger`. Prefer structured logging (Microsoft.Extensions.Logging) for production.

## Capabilities & limits

* ✅ Zero external deps besides core Shardis
* ✅ Thread-safe
* ⚠️ Not structured; plain text only
* ⚠️ Not suitable for high-volume production logging

## Samples & tests

See repository samples using a console logger. No specific tests (behavior is trivial).

## Versioning & compatibility

* Target frameworks: net8.0, net9.0
* Follows overall Shardis versioning.

## Contributing

PRs welcome. See <https://github.com/veggerby/shardis>.

## License

MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>

## Links

* Repo: <https://github.com/veggerby/shardis>
* Issues: <https://github.com/veggerby/shardis/issues>

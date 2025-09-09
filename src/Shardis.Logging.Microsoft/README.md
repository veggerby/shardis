# Shardis.Logging.Microsoft

Adapter from the Shardis logging abstraction to Microsoft.Extensions.Logging.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Logging.Microsoft.svg)](https://www.nuget.org/packages/Shardis.Logging.Microsoft/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Logging.Microsoft.svg)](https://www.nuget.org/packages/Shardis.Logging.Microsoft/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Install

```bash
dotnet add package Shardis.Logging.Microsoft --version 0.1.*
```

## When to use

* Integrate Shardis logs into existing ASP.NET Core / generic host logging
* Route logs to structured sinks (Serilog, OpenTelemetry, etc.)
* Maintain a single logging pipeline

## What’s included

* `MicrosoftLoggerAdapter` — wraps `ILogger`
* `ShardisMicrosoftLoggingExtensions.CreateShardisLogger` — convenience factory extension

## Quick start

```csharp
using Microsoft.Extensions.Logging;
using Shardis.Logging.Microsoft;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // build standard logging first
    })
    .Build();

var shardisLogger = host.Services.GetRequiredService<ILoggerFactory>()
                                 .CreateShardisLogger("Shardis");

shardisLogger.Log(Shardis.Logging.ShardisLogLevel.Information, "Topology loaded");
```

## Configuration / Options

Provide a custom level map:

```csharp
var shardisLogger = factory.CreateShardisLogger(levelMap: lvl => lvl switch {
    Shardis.Logging.ShardisLogLevel.Trace => LogLevel.Debug,
    _ => LogLevel.Information
});
```

## Integration notes

Inject the created adapter anywhere `IShardisLogger` is consumed. You may register it as a singleton.

## Capabilities & limits

* ✅ Reuses existing logging sinks
* ✅ Supports scopes (tags become a scope dictionary)
* ⚠️ Tags flattened to simple key/value
* ⚠️ Mapping function cost on each call (trivial)

## Samples & tests

See repository migration sample (once adapted) for usage.

## Versioning & compatibility

* Target frameworks: net8.0, net9.0
* Tracks Shardis core versions

## Contributing

PRs welcome. See <https://github.com/veggerby/shardis>.

## License

MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>.

## Links

* Repo: <https://github.com/veggerby/shardis>
* Issues: <https://github.com/veggerby/shardis/issues>

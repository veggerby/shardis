using System.Diagnostics;

namespace Shardis.Diagnostics;

/// <summary>
/// Centralized diagnostics constants and ActivitySource for Shardis.
/// </summary>
public static class ShardisDiagnostics
{
    /// <summary>
    /// The ActivitySource name used for tracing Shardis operations.
    /// </summary>
    public const string ActivitySourceName = "Shardis";

    /// <summary>
    /// Shared ActivitySource instance. Consumers should call <c>AddSource(ShardisDiagnostics.ActivitySourceName)</c>
    /// when configuring OpenTelemetry tracing to pick up Shardis spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Meter name used by the built-in metrics implementation.
    /// </summary>
    public const string MeterName = "Shardis";
}
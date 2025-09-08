namespace Shardis.Migration.Abstractions;

/// <summary>Projection context carrying optional schema version hints.</summary>
/// <param name="SourceSchemaVersion">Source schema version number (optional).</param>
/// <param name="TargetSchemaVersion">Target schema version number (optional).</param>
public readonly record struct ProjectionContext(int? SourceSchemaVersion, int? TargetSchemaVersion);
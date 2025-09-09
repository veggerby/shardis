namespace Shardis.Migration.Sql;

using System.Data.Common;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

/// <summary>
/// Durable checkpoint store backed by a single SQL table with upsert semantics.
/// Table schema (recommended):
///   CREATE TABLE ShardMigrationCheckpoint (
///       PlanId UNIQUEIDENTIFIER PRIMARY KEY,
///       Version INT NOT NULL,
///       UpdatedAtUtc DATETIME2 NOT NULL,
///       Payload NVARCHAR(MAX) NOT NULL -- JSON serialized checkpoint model
///   );
/// </summary>
/// <remarks>Experimental preview; replace ADO abstraction with concrete provider (e.g. Microsoft.Data.SqlClient) in consuming application.</remarks>
public sealed class SqlCheckpointStore<TKey>(Func<DbConnection> connectionFactory, string tableName = "ShardMigrationCheckpoint") : IShardMigrationCheckpointStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly Func<DbConnection> _connectionFactory = connectionFactory;
    private readonly string _table = tableName;

    /// <summary>Loads checkpoint by plan id or null if absent.</summary>
    public async Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Payload FROM {_table} WHERE PlanId=@id";
        AddParam(cmd, "@id", planId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null)
        {
            return null;
        }
        var json = (string)result;
        return System.Text.Json.JsonSerializer.Deserialize<MigrationCheckpoint<TKey>>(json);
    }

    /// <summary>Persists (upserts) the checkpoint state.</summary>
    public async Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(checkpoint);
        await using var conn = _connectionFactory();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"MERGE {_table} AS t
USING (SELECT @PlanId AS PlanId) AS s
ON (t.PlanId = s.PlanId)
WHEN MATCHED THEN UPDATE SET Version=@Version, UpdatedAtUtc=@UpdatedAtUtc, Payload=@Payload
WHEN NOT MATCHED THEN INSERT(PlanId, Version, UpdatedAtUtc, Payload) VALUES(@PlanId,@Version,@UpdatedAtUtc,@Payload);";
        AddParam(cmd, "@PlanId", checkpoint.PlanId);
        AddParam(cmd, "@Version", checkpoint.Version);
        AddParam(cmd, "@UpdatedAtUtc", checkpoint.UpdatedAtUtc.UtcDateTime);
        AddParam(cmd, "@Payload", json);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
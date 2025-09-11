namespace Shardis.Migration.Sql;

using System.Data.Common;

using Shardis.Model;
using Shardis.Persistence;

/// <summary>
/// SQL-backed shard map store with optimistic assignment and append-only history table.
/// Schema (suggested):
///   CREATE TABLE ShardMap (
///       ShardKey NVARCHAR(512) PRIMARY KEY,
///       ShardId NVARCHAR(128) NOT NULL,
///       Version ROWVERSION NOT NULL
///   );
///   CREATE TABLE ShardMapHistory (
///       Id BIGINT IDENTITY PRIMARY KEY,
///       ShardKey NVARCHAR(512) NOT NULL,
///       OldShardId NVARCHAR(128) NULL,
///       NewShardId NVARCHAR(128) NOT NULL,
///       ChangedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
///   );
/// </summary>
/// <summary>Experimental SQL-backed shard map store using provided connection factory for portability.</summary>
public sealed class SqlShardMapStore<TKey>(Func<DbConnection> connectionFactory, string mapTable = "ShardMap", string historyTable = "ShardMapHistory") : IShardMapStore<TKey>, IShardMapEnumerationStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly Func<DbConnection> _connectionFactory = connectionFactory;
    private readonly string _map = mapTable;
    private readonly string _history = historyTable;

    /// <summary>
    /// Raised after a new shard assignment is created (optimistic insert path). Old shard id will be <c>null</c> for current implementation
    /// because only insertion (no update) is supported in this experimental store.
    /// </summary>
    public event Action<ShardKey<TKey>, ShardId?, ShardId>? AssignmentChanged;

    /// <summary>Attempts to assign shard to key (insertion only). Returns true if inserted.</summary>
    public async Task<bool> TryAssignShardToKey(ShardKey<TKey> key, ShardId shardId)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {_map}(ShardKey, ShardId) VALUES(@k,@s)";
        AddParam(cmd, "@k", key.Value!.ToString()!);
        AddParam(cmd, "@s", shardId.Value);
        try
        {
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0 && key.Value is not null)
            {
                await InsertHistory(conn, key.Value.ToString()!, null, shardId.Value);
                AssignmentChanged?.Invoke(key, null, shardId);
                return true;
            }
        }
        catch { return false; }
        return false;
    }

    private async Task<ShardId?> TryGetShardForKeyInternal(ShardKey<TKey> key)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT ShardId FROM {_map} WHERE ShardKey=@k";
        AddParam(cmd, "@k", key.Value!.ToString()!);
        var result = await cmd.ExecuteScalarAsync();
        return result is string s ? new ShardId(s) : null;
    }

    /// <inheritdoc />
    public bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId)
    {
        var existing = TryGetShardForKeyInternal(shardKey).GetAwaiter().GetResult();
        if (existing is not null) { shardId = existing.Value; return true; }
        shardId = default!; return false;
    }

    /// <inheritdoc />
    public ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId)
    {
        // Non-atomic assign w/out race handling (preview). Prefer TryAssignShardToKey path.
        TryAssignShardToKey(shardKey, shardId).GetAwaiter().GetResult();
        return new ShardMap<TKey>(shardKey, shardId);
    }

    /// <inheritdoc />
    public bool TryAssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId, out ShardMap<TKey> shardMap)
    {
        var created = TryAssignShardToKey(shardKey, shardId).GetAwaiter().GetResult();
        shardMap = new ShardMap<TKey>(shardKey, shardId);
        return created;
    }

    /// <inheritdoc />
    public bool TryGetOrAdd(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, out ShardMap<TKey> shardMap)
    {
        if (TryGetShardIdForKey(shardKey, out var existing)) { shardMap = new ShardMap<TKey>(shardKey, existing); return false; }
        var created = valueFactory();
        TryAssignShardToKey(shardKey, created, out shardMap);
        return true;
    }

    private static async Task InsertHistory(DbConnection conn, string key, string? oldId, string newId)
    {
        await using var history = conn.CreateCommand();
        history.CommandText = "INSERT INTO ShardMapHistory(ShardKey,OldShardId,NewShardId) VALUES(@k,@o,@n)";
        AddParam(history, "@k", key);
        AddParam(history, "@o", (object?)oldId ?? DBNull.Value);
        AddParam(history, "@n", newId);
        await history.ExecuteNonQueryAsync();
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShardMap<TKey>> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT ShardKey, ShardId FROM {_map} ORDER BY ShardKey"; // ORDER BY for deterministic iteration
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var k = reader.GetString(0);
            var s = reader.GetString(1);
            yield return new ShardMap<TKey>(new ShardKey<TKey>((TKey)Convert.ChangeType(k, typeof(TKey))!), new ShardId(s));
        }
    }
}
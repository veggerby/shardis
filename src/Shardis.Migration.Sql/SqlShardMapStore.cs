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
public sealed class SqlShardMapStore<TKey>(Func<DbConnection> connectionFactory, string mapTable = "ShardMap", string historyTable = "ShardMapHistory") : IShardMapStoreAsync<TKey>, IShardMapEnumerationStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly Func<DbConnection> _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly string _map = ValidateTableName(mapTable);
    private readonly string _history = ValidateTableName(historyTable);

    /// <summary>
    /// Raised after a new shard assignment is created (optimistic insert path). Old shard id will be <c>null</c> for current implementation
    /// because only insertion (no update) is supported in this experimental store.
    /// </summary>
    public event Action<ShardKey<TKey>, ShardId?, ShardId>? AssignmentChanged;

    /// <inheritdoc />
    public async ValueTask<ShardId?> TryGetShardIdForKeyAsync(ShardKey<TKey> shardKey, CancellationToken cancellationToken = default)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT ShardId FROM {_map} WHERE ShardKey=@k";
        AddParam(cmd, "@k", shardKey.Value!.ToString()!);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is string s ? new ShardId(s) : null;
    }

    /// <inheritdoc />
    public async ValueTask<ShardMap<TKey>> AssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {_map}(ShardKey, ShardId) VALUES(@k,@s)";
        AddParam(cmd, "@k", shardKey.Value!.ToString()!);
        AddParam(cmd, "@s", shardId.Value);
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (shardKey.Value is not null)
        {
            await InsertHistory(conn, shardKey.Value.ToString()!, null, shardId.Value, cancellationToken);
            AssignmentChanged?.Invoke(shardKey, null, shardId);
        }
        
        return new ShardMap<TKey>(shardKey, shardId);
    }

    /// <inheritdoc />
    public async ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryAssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default)
    {
        await using var conn = _connectionFactory();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {_map}(ShardKey, ShardId) VALUES(@k,@s)";
        AddParam(cmd, "@k", shardKey.Value!.ToString()!);
        AddParam(cmd, "@s", shardId.Value);
        
        try
        {
            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows > 0 && shardKey.Value is not null)
            {
                await InsertHistory(conn, shardKey.Value.ToString()!, null, shardId.Value, cancellationToken);
                AssignmentChanged?.Invoke(shardKey, null, shardId);
                return (true, new ShardMap<TKey>(shardKey, shardId));
            }
        }
        catch
        {
            // Insert failed - key already exists, fetch existing value
            var existing = await TryGetShardIdForKeyAsync(shardKey, cancellationToken);
            if (existing is not null)
            {
                return (false, new ShardMap<TKey>(shardKey, existing.Value));
            }
        }
        
        return (false, new ShardMap<TKey>(shardKey, shardId));
    }

    /// <inheritdoc />
    public async ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryGetOrAddAsync(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, CancellationToken cancellationToken = default)
    {
        var existing = await TryGetShardIdForKeyAsync(shardKey, cancellationToken);
        if (existing is not null)
        {
            return (false, new ShardMap<TKey>(shardKey, existing.Value));
        }

        var newShardId = valueFactory();
        return await TryAssignShardToKeyAsync(shardKey, newShardId, cancellationToken);
    }

    /// <inheritdoc />
    public bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId)
    {
        throw new NotSupportedException("SqlShardMapStore requires async operations. Use TryGetShardIdForKeyAsync instead.");
    }

    /// <inheritdoc />
    public ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId)
    {
        throw new NotSupportedException("SqlShardMapStore requires async operations. Use AssignShardToKeyAsync instead.");
    }

    /// <inheritdoc />
    public bool TryAssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId, out ShardMap<TKey> shardMap)
    {
        throw new NotSupportedException("SqlShardMapStore requires async operations. Use TryAssignShardToKeyAsync instead.");
    }

    /// <inheritdoc />
    public bool TryGetOrAdd(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, out ShardMap<TKey> shardMap)
    {
        throw new NotSupportedException("SqlShardMapStore requires async operations. Use TryGetOrAddAsync instead.");
    }

    private static async Task InsertHistory(DbConnection conn, string key, string? oldId, string newId, CancellationToken cancellationToken = default)
    {
        await using var history = conn.CreateCommand();
        history.CommandText = "INSERT INTO ShardMapHistory(ShardKey,OldShardId,NewShardId) VALUES(@k,@o,@n)";
        AddParam(history, "@k", key);
        AddParam(history, "@o", (object?)oldId ?? DBNull.Value);
        AddParam(history, "@n", newId);
        await history.ExecuteNonQueryAsync(cancellationToken);
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

    /// <summary>
    /// Validates a table name to prevent SQL injection.
    /// Only allows alphanumeric characters, underscores, and dots (for schema-qualified names).
    /// </summary>
    private static string ValidateTableName(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName, nameof(tableName));
        
        // Allow only safe characters: letters, digits, underscores, and dots (for schema.table)
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[a-zA-Z0-9_\.]+$"))
        {
            throw new ArgumentException($"Invalid table name '{tableName}'. Only alphanumeric characters, underscores, and dots are allowed.", nameof(tableName));
        }
        
        return tableName;
    }
}
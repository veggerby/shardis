using Npgsql;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.Durable.Sample;

internal sealed class PostgresCheckpointStore<TKey>(string connectionString) : IShardMigrationCheckpointStore<TKey> where TKey : notnull, IEquatable<TKey>
{
    private readonly string _connectionString = connectionString;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = CreateOptions();

    private static System.Text.Json.JsonSerializerOptions CreateOptions()
    {
        var o = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false
        };
        o.Converters.Add(new ShardMigrationCheckpointConverter());
        return o;
    }

    public async Task<MigrationCheckpoint<TKey>?> LoadAsync(Guid planId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT payload FROM migration_checkpoint WHERE plan_id=@p", conn);
        cmd.Parameters.AddWithValue("p", planId);
        var payload = await cmd.ExecuteScalarAsync(ct);
        if (payload is null) return null;
        var json = payload as string ?? payload.ToString()!;
        return System.Text.Json.JsonSerializer.Deserialize<MigrationCheckpoint<TKey>>(json, JsonOptions);
    }

    public async Task PersistAsync(MigrationCheckpoint<TKey> checkpoint, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var json = System.Text.Json.JsonSerializer.Serialize(checkpoint, JsonOptions);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO migration_checkpoint (plan_id, version, updated_utc, payload) VALUES (@id,@v,@u,CAST(@p AS jsonb))
ON CONFLICT (plan_id) DO UPDATE SET version=EXCLUDED.version, updated_utc=EXCLUDED.updated_utc, payload=EXCLUDED.payload;", conn);
        cmd.Parameters.AddWithValue("id", checkpoint.PlanId);
        cmd.Parameters.AddWithValue("v", checkpoint.Version);
        cmd.Parameters.AddWithValue("u", checkpoint.UpdatedAtUtc.UtcDateTime);
        cmd.Parameters.AddWithValue("p", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed class ShardMigrationCheckpointConverter : System.Text.Json.Serialization.JsonConverter<MigrationCheckpoint<TKey>>
    {
        public override MigrationCheckpoint<TKey>? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            using var doc = System.Text.Json.JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var planId = root.GetProperty("PlanId").GetGuid();
            var version = root.GetProperty("Version").GetInt32();
            var updated = root.GetProperty("UpdatedAtUtc").GetDateTimeOffset();
            var statesElem = root.GetProperty("States");
            var states = new Dictionary<ShardKey<TKey>, KeyMoveState>();
            foreach (var item in statesElem.EnumerateArray())
            {
                var keyVal = item.GetProperty("Key").GetString()!;
                var key = new ShardKey<TKey>((TKey)Convert.ChangeType(keyVal, typeof(TKey))!);
                var stateElem = item.GetProperty("State");
                var stateStr = item.GetProperty("State").GetString()!;
                var state = Enum.Parse<KeyMoveState>(stateStr);
                states[key] = state;
            }
            var last = root.TryGetProperty("LastProcessedIndex", out var lpi) ? lpi.GetInt32() : -1;
            return new MigrationCheckpoint<TKey>(planId, version, updated, states, last);
        }

        public override void Write(System.Text.Json.Utf8JsonWriter writer, MigrationCheckpoint<TKey> value, System.Text.Json.JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("PlanId", value.PlanId);
            writer.WriteNumber("Version", value.Version);
            writer.WriteString("UpdatedAtUtc", value.UpdatedAtUtc);
            writer.WritePropertyName("States");
            writer.WriteStartArray();
            foreach (var kv in value.States)
            {
                writer.WriteStartObject();
                writer.WriteString("Key", kv.Key.Value?.ToString());
                writer.WriteString("State", kv.Value.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteNumber("LastProcessedIndex", value.LastProcessedIndex);
            writer.WriteEndObject();
        }
    }
}
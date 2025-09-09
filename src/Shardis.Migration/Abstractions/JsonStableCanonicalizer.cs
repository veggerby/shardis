using System.Buffers;

namespace Shardis.Migration.Abstractions;

/// <summary>Simple JSON canonicalizer using System.Text.Json with ordered properties.</summary>
public sealed class JsonStableCanonicalizer : IStableCanonicalizer
{
    private readonly System.Text.Json.JsonWriterOptions _writerOptions = new() { Indented = false, SkipValidation = true }; // deterministic minimal form

    /// <inheritdoc />
    public byte[] ToCanonicalUtf8(object value)
    {
        // NOTE: For now rely on property order defined by type metadata. For stronger guarantees a reflection-based
        // property ordering (alphabetical) could be introduced later.
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, _writerOptions))
        {
            System.Text.Json.JsonSerializer.Serialize(writer, value, value.GetType());
        }

        return buffer.WrittenSpan.ToArray();
    }
}
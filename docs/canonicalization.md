# Canonicalization & Checksum Verification

This document provides an in-depth reference for how Shardis derives stable, deterministic byte representations of migrated entities/documents for checksum verification.

---

## Goals

1. Deterministic: identical logical entity state -> identical byte sequence.
2. Minimal: avoid extraneous whitespace / formatting variance.
3. Stable across process boundaries / runtime upgrades (subject to documented invariants).
4. Extensible: callers can swap canonicalizer and hasher without changing pipeline logic.
5. Efficient: zero heap churn beyond the serialized payload + transient buffers.

---

## Components

| Component | Interface | Default | Responsibility |
|-----------|-----------|---------|----------------|
| Canonicalizer | `IStableCanonicalizer` | `JsonStableCanonicalizer` | Convert an object graph into a deterministic UTF-8 byte array. |
| Hasher | `IStableHasher` | `Fnv1a64Hasher` | Produce a stable, non-cryptographic 64-bit hash from canonical bytes. |
| Verification Strategy | `IVerificationStrategy<TKey>` | (Backend specific; e.g. Marten `DocumentChecksumVerificationStrategy<TKey>`) | Load source/target entities, project, canonicalize, hash, compare. |
| Projection Strategy | `IEntityProjectionStrategy` | `NoOpEntityProjectionStrategy` | Reduce / normalize entity shape prior to canonicalization. |

---

## Canonicalization Pipeline

```text
[Source Entity]
   ⭢ (Projection)            -> logical minimal view
   ⭢ (Canonicalizer)         -> UTF-8 byte[] (stable form)
   ⭢ (Hasher)                -> ulong (checksum)
   ⭢ (Compare Source/Target) -> equality -> verification result
```text

Each step is independently replaceable via DI.

---

## Default JSON Canonicalization (`JsonStableCanonicalizer`)

Implementation snapshot:

```csharp
// Simplified (see source for complete context)
public sealed class JsonStableCanonicalizer : IStableCanonicalizer {
    private readonly JsonWriterOptions _opts = new() { Indented = false, SkipValidation = true };
    public byte[] ToCanonicalUtf8(object value) {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, _opts);
        JsonSerializer.Serialize(writer, value, value.GetType());
        return buffer.WrittenSpan.ToArray();
    }
}
```

### Current Invariants

| Aspect | Rule | Rationale |
|--------|------|-----------|
| Encoding | UTF-8 | Industry default; efficient. |
| Whitespace | None (minified) | Eliminates formatting variance. |
| Property Order | Runtime reflection order (declared order) | Simplicity; stable for a given compiled type. |
| Null Values | Serialized per System.Text.Json defaults | Preserve semantic null vs missing distinctions. |
| Numbers | Canonical JSON numeric form (no trailing zeros) | Serializer-provided minimal form. |
| Booleans | `true` / `false` | JSON spec. |
| Strings | UTF-8, escaped per JSON spec | Deterministic escaping. |
| Date/Time | System.Text.Json default ISO 8601 | Widely interoperable. |
| Reference Loops | Not supported (throws) | Avoid silent truncation / nondeterminism. |

### Non-Goals (Current)

- Cryptographic collision resistance (use a cryptographic hash if required — pluggable).
- Cross-language canonical equivalence (guarantee is intra-.NET given same serializer & type version).
- Structural reordering normalization (alphabetical ordering is a possible future enhancement).

---

## Hashing (`Fnv1a64Hasher`)

| Property | Value |
|----------|-------|
| Algorithm | FNV-1a 64-bit (unsigned) |
| Purpose | Fast, stable, low allocation; not cryptographically secure |
| Collision Handling | On mismatch we only assert equality, collisions extremely unlikely for typical entity sizes, but not impossible |

You can provide an alternative (e.g., xxHash3, SHA-256) by registering your own `IStableHasher` before calling the migration provider DI extension.

---

## Projection (`IEntityProjectionStrategy`)

Projection shapes the entity into a minimal, semantically comparable structure (e.g., drop transient fields, reorder, flatten). Default is pass-through. Reasons to implement a custom projection:

- Exclude volatile fields (timestamps, etags).
- Normalize casing / culture-sensitive fields.
- Map backend-specific document wrappers to domain POCOs.
- Version bridge: project v1 + v2 documents into a common intermediate contract.

Projection must be pure and side-effect free.

---

## Verification Flow (Example – Marten)

1. Load source & target documents by key.
2. If either missing → fail verification.
3. Apply projection to both.
4. Canonicalize each projected object to UTF-8 bytes.
5. Hash each byte array.
6. Compare hashes; equality → verified.

This design isolates expensive operations and permits future optimizations (e.g., server-side hash precomputation) without altering executor logic.

---

## Extensibility Recipes

### Alphabetical Property Ordering

If reflection-order variance becomes a concern across build pipelines:

1. Implement a custom canonicalizer that walks object graphs via reflection.
2. For each object, gather properties (public instance), order by `Name`, serialize fields in that order into an incremental writer.
3. Reuse pooled buffers (`ArrayPool<byte>`) to minimize allocations.

### Cryptographic Hash

Register:

```csharp
services.AddSingleton<IStableHasher>(new Sha256Hasher());
```

Where `Sha256Hasher` implements:

```csharp
public sealed class Sha256Hasher : IStableHasher {
    public ulong Hash(ReadOnlySpan<byte> data) {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(data, digest);
        // Fold to 64-bit (xor lanes) or expose an extended interface returning 256 bits.
        ulong h = BitConverter.ToUInt64(digest);
        h ^= BitConverter.ToUInt64(digest[8..]);
        h ^= BitConverter.ToUInt64(digest[16..]);
        h ^= BitConverter.ToUInt64(digest[24..]);
        return h;
    }
}
```

(For strict cryptographic equality you may instead introduce `IStableHashProvider` returning full byte[] and adjust strategy.)

---

## Edge Cases & Guidance

| Scenario | Guidance |
|----------|----------|
| Missing source doc | Treat as verification failure; migration should not swap. |
| Missing target doc | Copy step likely failed; schedule re-copy. |
| Projection returns null | Treat as mismatch (cannot hash `null` vs object). |
| Numeric widening (int -> long) | Projection should normalize to a single type. |
| Floating point precision | Avoid direct floating comparison — project to decimal or string if invariants matter. |
| Time zones | Normalize to UTC in projection if domain uses mixed kinds. |
| Large payloads (>1MB) | Consider streaming canonicalizer or server-side checksum to avoid large allocations. |

---

## Versioning & Compatibility

Breaking changes to canonicalization (e.g., introducing alphabetical ordering) would invalidate prior stored checksums. Mitigation strategies:

- Versioned projection context (`ProjectionContext.SourceSchemaVersion` / `TargetSchemaVersion`).
- Dual-hash window: compute both old & new hash forms during a transition and accept either.
- Explicit migration flag: opt-in to upgraded canonicalization.

---

## Performance Considerations

| Factor | Impact | Mitigation |
|--------|--------|------------|
| Reflection cost | Moderate | Cache property metadata; avoid repeated lookups. |
| Allocation | Hashing requires full materialization | Use pooled writers or span-based hashing. |
| Large graphs | O(N) traversal | Limit projection depth; flatten early. |
| Hash collisions | Extremely low but non-zero | Escalate to structural compare on collision if critical. |

Benchmark additions (planned): measure canonicalization + hash throughput per 1k docs, track allocations.

---

## Planned Enhancements

1. Optional alphabetical property ordering mode.
2. Pooling / reusable buffer strategy for canonicalizer.
3. Pluggable server-returned checksum integration (e.g., SQL row hash, Marten metadata).
4. Dual-hash transitional support for upgrades.
5. Dedicated benchmark harness under `benchmarks/ChecksumBenchmarks.cs`.

---

## Quick Start (Override Components)

```csharp
services
    .AddShardisMigration<string>()
    .AddMartenMigrationSupport<string>()
    // Replace hasher
    .AddSingleton<IStableHasher, MyXxHash64Hasher>()
    // Replace canonicalizer
    .AddSingleton<IStableCanonicalizer, AlphabeticalJsonCanonicalizer>()
    // Replace projection
    .AddSingleton<IEntityProjectionStrategy, OrderProjectionStrategy>();
```

---

## Summary

Canonicalization in Shardis is intentionally simple and pluggable: project -> canonicalize -> hash. The defaults favor determinism and low overhead while keeping room for stronger guarantees when needed. Treat the defaults as a baseline; customize when domain volatility or compliance requirements demand tighter controls.

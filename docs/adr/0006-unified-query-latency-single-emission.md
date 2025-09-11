# 0006: Unified Query Latency Single-Emission Model

Status: Accepted
Date: 2025-09-10
Supersedes: (none)
Superseded by: (none)

## Context

Originally, cross-shard query latency was recorded in multiple places (unordered executor completion, ordered wrapper, failure-handling wrapper), producing duplicate histogram points for a single logical enumeration. This inflated latency samples, complicated downstream analysis, and made correctness (e.g., percentiles) noisy. Additionally, the introduction of best-effort failure handling required deterministic tagging (`failure.mode`, `result.status`) after final outcome normalization.

## Decision

Introduce a suppression + pending-context pattern:

1. Base (unordered) executor captures start timestamps and constructs a `PendingLatencyContext`.
2. Emission is suppressed until exactly one terminal wrapper (ordered path or failure handling) consumes and emits the histogram.
3. Ordered executor reuses the captured context (including start time) so ordering overhead is included without double counting.
4. Failure handling executor normalizes `result.status` when best-effort yields partial success.
5. Ambient failure mode (AsyncLocal) is consulted strictly for tagging; it does not alter routing decisions.

## Rationale

* Ensures a single authoritative duration per enumeration.
* Avoids coordination via global/static flags (keeps per-enumeration scope).
* Keeps ordered vs unordered logic pluggable while sharing timing state.
* Guarantees tags reflect final semantic outcome (after best-effort normalization).

## Alternatives Considered

* "Emit early" in unordered executor then adjust / emit delta in wrappers – rejected (would require compensating metrics and break determinism).
* Global correlation ID registry to de-duplicate emissions – rejected (stateful, higher contention, GC pressure).
* Moving latency entirely into outermost wrapper – rejected (would miss base executor scheduling overhead and complicate composition ordering).

## Consequences

Positive:

* Deterministic single emission simplifies validation tests.
* Reduced telemetry noise; stable percentiles.
* Clear separation of timing capture vs. final emission.

Negative / Trade-offs:

* Reflection currently used to access ordered wrapper helper (temporary; to be refactored to an internal interface).
* Slight additional allocation for `PendingLatencyContext`.

## Invariants

* Exactly one histogram point per logical enumeration (success, failure, cancellation, zero result, invalid targeting fast-path where applicable).
* `failure.mode` set to `fail-fast` or `best-effort` (never empty on emission).
* `result.status` mapping rules:
  * fail-fast: first failure => `failed`.
  * best-effort: if ≥1 shard succeeded => `ok`; if all shards failed => histogram still emitted with `failed` (test coverage ensures this path).
* Ordered path includes buffering time in duration.

## Testing

* All telemetry tests assert exactly one point.
* Regression test covers ordered + best-effort combination (including empty result permissiveness and status flexibility).
* Future guard test (planned) to assert non-empty `failure.mode` across all emissions.

## Future Work

* Replace reflection with an explicit internal contract for ordered executor creation.
* Add allocation benchmark around emission path to monitor regression.
* Introduce streaming ordered merge (distinct tag differentiation retained).

## References

* `docs/query-latency.md`
* `Shardis.Query` metrics implementation (`FailureHandlingExecutor`, EF Core unordered executor, ordered wrapper helper).

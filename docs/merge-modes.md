# Merge & Streaming Modes

| Mode | Ordering | Memory Profile | First Item Latency | Flow Control Parameter | When To Use |
|------|----------|----------------|--------------------|------------------------|-------------|
| Unordered Streaming | None (arrival order) | O(shards + channelCapacity) | Lowest | BackpressureCapacity (channel) | Firehose fan-out, low latency analytics, early partial consumption |
| Ordered Streaming | Global (k-way heap) | O(shards × PrefetchPerShard) | Low (after one item per shard) | PrefetchPerShard (heap budget) | Real-time sorted feeds, merges where memory must stay bounded |
| Ordered Eager | Global | O(total items) | High (must materialize all) | N/A | Small result sets where simplicity outweighs latency/memory |

## Flow Control

| Dimension | Unordered Streaming | Ordered Streaming |
|-----------|--------------------|-------------------|
| Backpressure Mechanism | Bounded channel capacity (producer blocks on full buffer) | Per-shard prefetch budget (enumerator stops pulling when buffered items == PrefetchPerShard) |
| Tuning Knob | `channelCapacity` (constructor) | `prefetchPerShard` (method param) |
| Primary Trade-off | Higher capacity increases throughput but raises memory & tail latency | Higher prefetch increases heap occupancy & memory but can smooth slow shard stalls |

## Tuning Prefetch

Guidance:

* 1 (default): Lowest memory & fastest time-to-first-item. Throughput can dip if shards are imbalanced.
* 2: Good middle ground – allows overlap when one shard pauses briefly.
* 4: Throughput oriented; only increase if profiling shows frequent heap starvation (empty between TopUps).

Memory scales as: `O(shards × prefetchPerShard)` items resident in the heap.

## Selecting a Mode

* Prefer Unordered Streaming unless consumers require global ordering semantics.
* Prefer Ordered Streaming over Eager for large or unbounded streams – it emits progressively and bounds memory.
* Use Ordered Eager only for small bounded result sets where simplicity and single-pass sort cost is negligible.

### LINQ MVP Integration

The minimal LINQ provider (`Shardis.Query`) currently emits only unordered streams (Where/Select). To obtain ordered results combine with broadcaster ordered streaming APIs after materialization or delay adopting ordering until provider vNext (see ADR 0003). Example:

```csharp
var exec = /* IShardQueryExecutor */;
var unordered = Shardis.Query.ShardQuery.For<Person>(exec)
 .Where(p => p.Age > 40)
 .Select(p => p.LastSeen);
// unordered enumeration
await foreach (var ts in unordered) { /* ... */ }
```

## Observer & Metrics

`IMergeObserver` receives callbacks for item yield, shard completion (success only), shard stopped (any terminal state), backpressure waits and heap size samples. Wire adapters to Prometheus/OpenTelemetry by translating:

| Callback | Suggested Metric |
|----------|------------------|
| OnItemYielded | Counter: `shardis.merge.items_total{shard}` |
| OnShardCompleted | Histogram/summary: `shardis.merge.shard_duration_seconds{shard}` (duration tracked externally) |
| OnShardStopped(reason) | Counter by reason: `shardis.merge.shards_stopped_total{reason}` |
| OnBackpressureWaitStart/Stop | Counter (increment on Stop): `shardis.merge.backpressure_blocks_total` & timer for blocked seconds |
| OnHeapSizeSample | Gauge: `shardis.merge.heap_size` |

Sampling frequency for heap size is per inserted item by default; you can reduce via broadcaster `heapSampleEvery` constructor arg or down-sample in the adapter.

### Quick Defaults

Start here, then profile:

* Ordered Streaming: `prefetchPerShard = 1` (bump to 2–4 only if heap often empties while shards still producing).
* Unordered Streaming: `channelCapacity = 256` (halve/double while watching backpressure wait count & total blocked time).
* If backpressure wait count is high (> a few per second) and blocked time grows, increase capacity (unordered) or prefetch (ordered) modestly.
* Track two core dials via observer: total blocked duration (sum of wait windows) and number of waits.

## Streaming & Fairness Guarantees (Tested)

Validated via deterministic test suite (`StreamingMergeTests`):

* Streaming ordered merge yields items before slow shards finish (`OrderedStreaming_YieldsBeforeSlowShardCompletes`).
* Unordered streaming maintains progress for fast shards under harsh skew; no starvation beyond bounded dry spells (`UnorderedStreaming_FastShards_AreNotStarved_UnderSkew`).
* Ordered eager path defers first yield until full materialization (`OrderedEager_DoesNotYieldUntilMaterializationCompletes`).
* Channel capacity variations (32 / 128 / 512) do not induce starvation; longest observed fast-shard dry spell scales within a small multiple of capacity (`UnorderedStreaming_CapacityDoesNotCauseStarvation`).

Guarantees rely on fixed seed (1337), deterministic per-shard delay schedules (no wall-clock sleeps), and cancellation timeouts to ensure reliability in CI.

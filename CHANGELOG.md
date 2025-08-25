# Changelog

All notable changes to the `Shardis.*` packages will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_No changes yet._

## [0.1.0] - 2025-08-25

### Added

- Default modulo-based routing (`DefaultShardRouter`).
- Consistent hashing router with virtual nodes and dynamic topology (`AddShard` / `RemoveShard`) + atomic ring snapshot.
- Pluggable key hashing & ring hashing abstractions with default + FNV-1a implementations.
- In-memory & Redis shard map stores with atomic `TryAssignShardToKey` and `TryGetOrAdd`.
- Shard broadcasting primitives (`ShardBroadcaster`, `ShardStreamBroadcaster`) and ordered / combined async enumerators.
- Metrics abstraction (`IShardisMetrics`) and single-miss routing metrics invariant.
- Migration scaffolding (planning & execution skeleton).
- Benchmark project (routers, hashers, streaming) + docs.
- DI extension (`AddShardis`) with configurable replication factor, hashers, store factory, optional custom router factory, preserves existing `IShardMapStore` / `IShardisMetrics` registrations.
- Replication factor upper bound validation (≤ 10,000).
- Fallback reassignment for removed shard mappings.
- Comprehensive test additions (replication factor validation, concurrency, dynamic ring rebuild, metrics invariants, ring distribution, broadcaster cancellation).
- Documentation: dynamic topology, metrics semantics, ring rebuild atomicity, CAS optimization, backlog & changelog.

### Changed

- Unified default routing path with per-key lock (single miss guarantee).
- Consistent router now uses `TryGetOrAdd` and records miss only on first assignment.
- Metrics semantics tightened (miss only on creation; hit differentiates new vs existing).
- Service registration refactored: consistent hashing router is now the default (unless disabled or custom factory supplied) and existing map store / metrics registrations are no longer overridden.

### Fixed

- Multiple `RouteMiss` emissions under high concurrency.
- KeyNotFound after shard removal (automatic reassignment).
- Metrics test flakiness (thread-safe collection).

### Internal / Quality

- Single miss invariant; replication cap; shard ID uniqueness validation.
- Atomic ring snapshot swap invariant & tests.
- Backlog expanded (performance, migration, observability, memory strategy).

### Deprecated

- None.

### Removed

- None.

### Notes

- Dynamic topology limited to consistent hashing router; default router static to avoid full rehash churn.
- Per-key lock & miss tracking dictionaries currently unbounded (future lifecycle strategy).

---
Template categories follow Keep a Changelog conventions (Added / Changed / Fixed / etc.). Once merged, tag under the next version (e.g., `0.2.0` if previous was `0.1.x`).

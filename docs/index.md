# Shardis Documentation Index

This index links core conceptual and roadmap documents.

## Core Concepts

- Routing & Hashing: see README sections (Deterministic Routing, Dependency Injection Options).
- Metrics Integration: README Metrics section + [Query Merge Latency](./query-latency.md)
- Migration scaffolding: README Migration section.
- Health & Resilience: [Health & Resilience Runtime](./health-resilience.md) (health monitoring, query routing, failure strategies)
- Terminology Glossary: `terms.md` (canonical Shardis & domain vocabulary)
- Migration Rationale: `migration-rationale.md` (why a formal mechanism exists)

## Detailed Design / Roadmap Docs

- Fluent Query API Vision: `api.md`
- Fluent API Implementation Plan: `api-plan.md`
- LINQ Query Architecture & Orchestrator: `linq.md`
- Backlog & Feature Roadmap: `backlog.md`
- Benchmarks & Performance Guidance: `benchmarks.md`
- Canonicalization & Checksums: `canonicalization.md`

## Contribution

- AI / Automation Guidelines: `.github/copilot-instructions.md`
- General contribution workflow: `../CONTRIBUTING.md`

## Status Legend

- ✅ Implemented
- 🧪 Experimental / Prototype
- 🚧 Planned / In Progress

| Area | Status | Reference |
|------|--------|-----------|
| Default routing | ✅ | README |
| Consistent hashing router | ✅ | README |
| Metrics (no-op + counters) | ✅ | README / `IShardisMetrics` |
| Health & resilience runtime | ✅ | `health-resilience.md` |
| Migration planning (in-memory + segmented) | ✅ | README / migration usage |
| Migration execution | ✅ | README / migration usage |
| Migration dry-run (counts) | ✅ | migration usage |
| Fluent query API | 🚧 | `api.md`, `linq.md` |
| Redis shard map store | ✅ | `Shardis.Redis` project |
| Additional map stores (SQL) | 🚧 | backlog |
| Benchmarks suite | ✅ | `benchmarks.md` |
| Ordered merge enumerator | ✅ | tests / README |
| Adaptive paging (Marten) | ✅ | README (Adaptive Paging) |
| Public API snapshots | ✅ | test/Shardis.PublicApi.Tests |

This file will evolve as components progress through stages.

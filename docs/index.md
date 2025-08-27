# Shardis Documentation Index

This index links core conceptual and roadmap documents.

## Core Concepts

- Routing & Hashing: see README sections (Deterministic Routing, Dependency Injection Options).
- Metrics Integration: README Metrics Integration section.
- Migration scaffolding: README Migration section.

## Detailed Design / Roadmap Docs

- Fluent Query API Vision: `api.md`
- Fluent API Implementation Plan: `api-plan.md`
- LINQ Query Architecture & Orchestrator: `linq.md`
- Backlog & Feature Roadmap: `backlog.md`
- Benchmarks & Performance Guidance: `benchmarks.md`

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
| Migration planning | ✅ (scaffold) | README |
| Migration execution | 🚧 | README / backlog |
| Fluent query API | 🚧 | `api.md`, `linq.md` |
| Redis shard map store | ✅ | `Shardis.Redis` project |
| Additional map stores (SQL) | 🚧 | backlog |
| Benchmarks suite | ✅ | `benchmarks.md` |
| Ordered merge enumerator | ✅ | tests / README |
| Adaptive paging (Marten) | ✅ | README (Adaptive Paging) |
| Public API snapshots | ✅ | test/Shardis.PublicApi.Tests |

This file will evolve as components progress through stages.

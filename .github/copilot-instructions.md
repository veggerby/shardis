---
applyTo: '**'
---
<!--
GitHub Copilot / AI Contributor Instructions for the Shardis codebase.
These rules are binding for any AI-generated contribution.
-->

# Shardis – AI Contribution Guidelines

Shardis is a production-focused .NET sharding framework. All AI assistance must produce code that is correct, deterministic, maintainable, and aligned with existing architectural principles. Humor or playful tone from other templates is not appropriate here.

---
## 1. Non‑Negotiable Principles
1. Determinism over cleverness.
2. Clarity over abstraction bloat (but keep extensibility points explicit).
3. Safety: thread-safe routing & persistence components.
4. No leakage of sharding concerns into domain models.
5. Public surface must be documented and stable.

---
## 2. Style & Formatting
Follow `.editorconfig` exactly.

Mandatory:
- File-scoped namespaces only.
- Explicit namespaces mirroring folders (`Shardis.Hashing`, `Shardis.Routing`, etc.).
- Full curly braces for every control block.
- `async/await` (no `Result`, `Wait()`, or sync over async).
- Nullable reference types respected; no `#nullable disable` unless justified.
- Prefer `readonly record struct` for immutable value objects (e.g. keys, ids).
- Private fields: `_camelCase`; public types/members: PascalCase.
- Remove unused usings; keep ordering consistent.
- Primary constructors where useful.
- Whitespace matters:
  - Separate logical blocks clearly with vertical space.
  - Use vertical space generously for test sections.

Prohibited:
- Generated banner comments / AI disclaimers.
- Region folding (`#region`) unless already established pattern (not currently used).

---
## 3. Architectural Boundaries
- Models (`Shard`, `ShardKey`, `ShardId`) are pure data holders; no infrastructure logic.
- Routers encapsulate selection logic (`DefaultShardRouter`, `ConsistentHashShardRouter`).
- Persistence abstractions: `IShardMapStore<TKey>`; implementations must be pluggable and thread-safe.
- Hashing abstractions: `IShardKeyHasher<TKey>` (key hashing) & `IShardRingHasher` (ring hashing). Never hardcode hashing strategies inside business logic.
- Metrics: use `IShardisMetrics`; default is no-op; do not depend on concrete implementation.
- Querying & broadcasting must remain streaming-first; avoid materializing whole shard result sets unless required by terminal operators.

Rules:
- No randomness in routing decisions (except deterministic hash functions).
- Any stateful router caches must be concurrency-safe.
- Avoid static mutable state.

---
## 4. Testing Requirements
Every new core component (routing, hashing, map store, migrator, enumerator, metrics adapter) must include unit tests under `test/Shardis.Tests/` using xUnit.

All generated tests **must**:

- Use `NSubstitute` for mocking — never `Moq`
- Use `AwesomeAssertions` for fluent assertions — never `FluentAssertions`
- Deterministic inputs (seeded random if necessary; expose seed constant).
- Be divided with `// arrange`, `// act`, `// assert` comments and proper spacing
- Cover: happy path, edge case (empty shards, single shard), error/exception path, concurrency or idempotency where relevant.
- Use fakes or in-memory implementations (e.g. `InMemoryShardMapStore`) over real external services in unit tests.

---
## 5. Performance & Benchmarks
Benchmarks live in `benchmarks/` using BenchmarkDotNet.
- Add benchmarks when optimizing hashing, routing, or merge enumerators.
- Do not regress allocations in hot paths without justification & note in PR.

---
## 6. Documentation
All public APIs require XML docs including `<summary>` and parameter docs. For complex algorithms (e.g. consistent hashing ring management), include a concise `<remarks>` describing invariants.
Update `README.md` / `docs/` when adding: new DI options, public abstractions, or end-user features.

---
## 7. Dependency Injection (DI)
- Prefer constructor injection; no service locator patterns.
- `ServiceCollectionExtensions.AddShardis` is the single composition entry point—extend via options before adding new overloads.
- Do not silently override user-registered services; check for existing registrations first.

---
## 8. Forbidden Patterns
🚫 Console output in library projects (samples only).
🚫 Business logic in model/value types.
🚫 Blocking waits (`.Result`, `.Wait()`).
🚫 Hidden side effects or ambient singletons.
🚫 Unbounded growth caches without eviction or documentation.
🚫 Random shard assignment.

---
## 9. Migration & Future Work (Scaffolding)
When extending migration (`IShardMigrator`):
- Keep planning vs execution distinct.
- Ensure idempotency: re-running a plan should not duplicate work.
- Document data integrity guarantees.

---
## 10. Metrics & Observability
- Never hardcode metric export logic in routers; use `IShardisMetrics`.
- Expose counters for: route hit, miss (new assignment), existing assignment.
- Keep metric calls outside of critical lock sections where possible.

---
## 11. Pull Request Checklist (AI-Generated Code)
Before submitting, validate:
- [ ] Build passes (`dotnet build`).
- [ ] Tests added / updated (`dotnet test`).
- [ ] Full solution test suite executed (no relying on filtered subsets) immediately before declaring task complete.
- [ ] Benchmarks unaffected or justified (if touching critical path).
- [ ] Public APIs documented.
- [ ] No style warnings introduced.
- [ ] Determinism preserved (hashing, routing).
- [ ] Thread-safety maintained or documented.
- [ ] README / docs updated if user-facing change.

---
## 12. Example Acceptable Prompt Targets
Suitable tasks for Copilot:
- "Add a xxHash-based IShardRingHasher and corresponding benchmark & tests."
- "Implement migration execution step with dry-run option and tests."
- "Add ordered streaming merge benchmark comparing current vs optimized enumerator."

Unsuitable (reject or ask for clarification):
- Vague feature requests without architectural context.
- Requests to bypass tests or docs.

---
## 13. Failure Handling
If information is missing:
1. Infer minimal, reasonable defaults consistent with existing patterns.
2. Clearly mark assumptions in PR description.
3. Provide follow-up items if larger design decisions are needed.

---
## 14. Security & Safety
- Do not log key material or shard assignments in production code by default.
- Validate external inputs to public extension methods.

---
## 15. Final Reminder
If an AI suggestion increases complexity without measurable benefit (performance, clarity, extensibility), **reject it**. Shardis prioritizes stable, predictable infrastructure primitives over novelty.

---
End of guidelines.

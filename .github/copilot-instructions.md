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
When extending migration (use `Shardis.Migration` extensions / `IShardMigrationPlanner` / `ShardMigrationExecutor`):
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

## Universal README structure (for all Shardis.\* packages)

1. **Title + one-line value prop**
   Short, specific, and outcome-oriented.

2. **Badges (optional but nice)**
   Build, NuGet version/downloads, license, docs link.

3. **Install**
   The exact `dotnet add package ...` inc. version wildcard policy.

4. **When to use**
   Bulleted, 2–5 bullets; user-facing criteria (not tech specs).

5. **What’s included**
   Concise bullets: main types, interfaces, extension methods, utilities.

6. **Quick start**
   10–20 lines of runnable code that demonstrates the *happy path*.

7. **Configuration / Options**
   How to tweak behavior (constructor args, options objects, DI, env vars).

8. **Integration notes**
   How this package fits with the rest of Shardis (related packages, required interfaces, typical wiring).

9. **Capabilities & limits**
   What it does well, known trade-offs, version compatibility, performance notes.

10. **Samples & tests**
    Point to sample projects and relevant test folders.

11. **Versioning & compatibility**
    Target frameworks, supported Shardis versions, breaking-change notes.

12. **Contributing**
    Link to CONTRIBUTING, coding style (SHIT-compliant), how to run tests.

13. **License**
    Short line + link.

14. **Links**
    Docs site, API reference, issue tracker, discussions.

NB! Do **not** add repository relative links in the package README.md. They will break on Nuget.org. Ensure they are full links to <https://github.com/veggerby/shardis> GitHub repository.

---

### Reusable README template

````markdown
# <PackageName>

<One-sentence value proposition: what problem it solves and for whom.>

[![NuGet](https://img.shields.io/nuget/v/<PackageName>.svg)](https://www.nuget.org/packages/<PackageName>/)
[![Downloads](https://img.shields.io/nuget/dt/<PackageName>.svg)](https://www.nuget.org/packages/<PackageName>/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Install

```bash
dotnet add package <PackageName> --version <x.y.*>
````

## When to use

* \<bullet 1>
* \<bullet 2>
* \<bullet 3>

## What’s included

* `<TypeOrInterface>` — <short description>
* `<HelperOrExtension>` — <short description>
* …

## Quick start

```csharp
// Minimal, runnable example showing the happy path.
```

## Configuration / Options

* **OptionA**: what it does, default value
* **OptionB**: how to set (DI / ctor / fluent), example:

```csharp
services.AddShardis(/* ... */)
        .Add<ThisPackage>(o => { o.PageSize = 256; });
```

## Integration notes

* Works with: \<related Shardis packages / storage engines / frameworks>
* Requires: `<IWhatever>` implementation (provided by \<this/other> package)
* Typical wiring: \<one sentence + link/code>

## Capabilities & limits

* ✅ <capability>
* ⚠️ \<known limitation / trade-off>
* 🧩 Compatibility: .NET <TFMs>, Shardis <version range>

## Samples & tests

* Samples: `path/to/samples`
* Tests: `path/to/tests`

## Versioning & compatibility

* Target frameworks: `net8.0`, `net9.0`
* Semantic versioning: **Minor** may add features; **Major** may break.
* See CHANGELOG for details.

## Contributing

PRs welcome. Please read [CONTRIBUTING.md](../CONTRIBUTING.md) and follow SHIT-compliant style.

## License

MIT — see [LICENSE](../LICENSE).

## Links

* Docs: <link>
* Issues: <link>
* Discussions: <link>

````

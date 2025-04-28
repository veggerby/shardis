# Copilot / AI Contributor Instructions for Shardis

Thank you for using Copilot, GPT, or any AI-assisted tools to contribute to Shardis.

Please follow these mandatory guidelines when using AI to generate code for this project.

---

## ✨ General Expectations

- AI suggestions are **welcome** but **must match human-written code quality**.
- Code must feel **clean, professional, and maintainable** — no obvious AI artifacts or shortcuts.
- AI-written code should be treated as a **starting point**, then manually reviewed, polished, and explained clearly.

---

## 🛠️ Coding Standards

✅ **Adhere Strictly to `.editorconfig`**

- Shardis defines style rules in the root `.editorconfig` file.
- All AI-generated code must fully comply without exceptions.

✅ **Follow Standard .NET/C# Practices**

- Namespace layout must match folder structure (`Shardis.Model`, `Shardis.Routing`, etc.).
- Use **full curly braces** `{}` even for single-line `if`, `for`, or `while` blocks.
- Use **`async/await`** naturally — no synchronous hacks around async APIs.
- Prefer **readonly structs** for immutable value objects.
- Model behavior into **small, composable classes** — avoid God-objects.
- Always prefer **dependency injection** over static helpers.
- Favor **interfaces** for externally visible services (e.g., `IShardRouter`, `IShardMapStore`).
- Public API methods must include clear **XML documentation comments**.

✅ **Consistency is Mandatory**

- Keep file and type names **PascalCase**.
- Keep private fields **camelCase** prefixed with underscore (`_`).
- No random style deviations, even minor.

---

## 🔍 Architectural Guidelines

- **Separate concerns cleanly**:
  - Models (`Shard`, `ShardKey`) must be pure and behavior-free.
  - Routers (`DefaultShardRouter`, `ConsistentHashShardRouter`) must be infrastructure-level services.
  - Persistence logic (`IShardMapStore`) must remain pluggable.
- **Never leak shard logic into domain models** (aggregates should not "know" about shards).
- **Routing decisions must be deterministic** — no randomization unless explicitly documented.
- **Router implementations must be thread-safe** if mutating internal state.

---

## 🧪 Testing Expectations

- Unit tests are mandatory for core components.
- Tests must be readable, isolated, and not overly abstracted.
- Use Arrange-Act-Assert structure in test methods.
- Mock dependencies where reasonable (e.g., use fake `IShardMapStore`).

---

## ❗ Forbidden Patterns

🚫 No generated files or "playground" code inside `/Shardis` or `/Shardis.Tests`.
🚫 No console output in library code (only in `/SampleApp`).
🚫 No business logic inside model classes (`Shard`, `ShardKey`).
🚫 No magic strings or "clever" hacks — be clear and boring when necessary.
🚫 No copying random snippets from StackOverflow without understanding.

---

## 📋 Pull Request Expectations

- Always manually review AI-written code for quality and clarity.
- Explain in PR description whether Copilot/AI was involved and where.
- Highlight any parts that might need special attention during code review.

---

# ✨ Final Reminder

Shardis is designed to **scale with simplicity, determinism, and clarity**.
If an AI suggestion makes the system *more confusing*, *harder to extend*,

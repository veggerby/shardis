# Contributing to Shardis

First off — thank you for considering contributing to Shardis!
This project exists because of people like you who want to build better systems, not just bigger ones.

Shardis is still early in its journey, and every contribution — big or small — makes a real difference.

---

## 🚀 How to Contribute

There are many ways you can help:

- **Report bugs**: Found something strange or broken? Open an issue.
- **Suggest improvements**: Ideas for features, extensions, better defaults — always welcome.
- **Improve documentation**: Good docs make the project more accessible to everyone.
- **Submit code changes**: Fix a bug, add a feature, or improve internal structure.

---

## 📋 Contribution Guidelines

Please follow these basic guidelines to keep everything smooth:

1. **Open an Issue First**
   If you're planning a larger change, open an issue first to discuss it. It helps avoid duplicated work or big surprises.

2. **Small Pull Requests**
   Try to keep PRs focused and easy to review. Smaller changes get merged faster.

3. **Write Tests**
   If you're fixing a bug or adding a new feature, please include or update tests to cover it.
   - **Unit tests**: Fast, no external dependencies (run with `dotnet test --filter "Category!=Integration"`)
   - **Integration tests**: Require Docker, use Testcontainers (run with `dotnet test --filter "Category=Integration"`)
   - Tag integration tests with `[Trait("Category", "Integration")]` at the class level
   - See [`docs/testing/integration-tests.md`](docs/testing/integration-tests.md) for integration testing guide

4. **Match the Code Style**
   Keep code clean and consistent.
   - Use full curly braces `{}` for conditionals and loops.
   - Use async/await naturally where needed.
   - Prefer immutability for models and value objects when possible.

5. **Explain Your Changes**
   In PRs, explain the "why" as well as the "what". A few clear sentences are enough.

---

## 🛠 Local Setup

- Clone the repository.
- Build the solution (`Shardis.sln`) using .NET 9 or later.
- Run the tests to make sure everything passes before you push.

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run only unit tests (fast)
dotnet test --filter "Category!=Integration"

# Run only integration tests (requires Docker)
dotnet test --filter "Category=Integration"
```

**For integration tests**: Docker must be installed and running. Testcontainers will automatically manage containerized dependencies (Redis, PostgreSQL).

---

## 🧩 Project Structure (Overview)

| Folder | Purpose |
|:-------|:--------|
| `/Shardis` | Core library: routing, models, persistence abstractions |
| `/Shardis.Tests` | Unit and integration tests |
| `/SampleApp` | Example usage (coming soon) |

---

## 🛡️ Code of Conduct

Be kind.
Shardis welcomes contributors from all backgrounds and skill levels. No toxicity, no gatekeeping. We’re here to build together.

---

## 📢 Final Thoughts

Shardis isn't trying to reinvent databases.
It’s about making scalable design accessible to every .NET developer, without the usual pain.

Thank you for helping make that happen.

— The Shardis Team

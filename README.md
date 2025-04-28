# Shardis

> **Shardis**: _Bigger on the inside. Smarter on the outside._

**Shardis** is a lightweight, scalable sharding framework for .NET designed to help developers partition and route aggregates across multiple databases cleanly and efficiently.
Built for domain-driven systems, event sourcing architectures, and multi-tenant platforms, Shardis ensures that data routing remains deterministic, maintainable, and completely decoupled from business logic.

![Shardis](https://img.shields.io/badge/Shardis-Shard%20Routing%20for%20.NET-blueviolet?style=flat-square)

---

## ✨ Features

- 🚀 **Deterministic Key-based Routing**
  Route aggregate instances consistently to the correct database shard based on a strong hashing mechanism.

- 🛠️ **Pluggable Shard Map Storage**
  Abstract where and how shard mappings are stored — support in-memory development, persistent stores, or distributed caches.

- 🔗 **Designed for Event Sourcing and CQRS**
  Integrates naturally with systems like MartenDB, EventStoreDB, and custom event stores.

- 🧩 **Simple, Extensible Architecture**
  Swap out routing strategies or extend shard metadata without leaking sharding concerns into your domain models.

- 🏗 **Ready for Production Scaling**
  Shard assignments are persistent, predictable, and optimized for horizontal scalability.

---

## 📦 Installation

🔜*(Coming soon to NuGet.)*

For now, clone the repository:

```bash
git clone https://github.com/veggerby/shardis.git
cd Shardis
```

Reference the Shardis project in your solution, or package it locally using your preferred method.

---

## 🚀 Getting Started

Setting up a basic router:

```csharp
using Shardis.Model;
using Shardis.Routing;
using Shardis.Persistence;

// Define available shards
var shards = new[]
{
    new SimpleShard(new("shard-001"), "postgres://user:pass@host1/db"),
    new SimpleShard(new("shard-002"), "postgres://user:pass@host2/db"),
    new SimpleShard(new("shard-003"), "postgres://user:pass@host3/db")
};

// Initialize the shard router
var shardRouter = new DefaultShardRouter(
    shardMapStore: new InMemoryShardMapStore(),
    availableShards: shards
);

// Route a ShardKey
var userId = new ShardKey("user-451");
var shard = shardRouter.RouteToShard(userId);

Console.WriteLine($"User {userId} routed to {shard.ShardId}");
```

---

## 🧠 How It Works

1. **ShardKey**: A value object representing the identity of an aggregate or entity to be routed.
2. **Shard**: Represents a physical partition (e.g., a specific PostgreSQL database instance).
3. **ShardRouter**: Routes incoming ShardKeys to the appropriate Shard based on hashing.
4. **ShardMapStore**: Caches key-to-shard assignments to ensure stable, deterministic routing over time.

---

## 📚 Example Use Cases

- Distribute user accounts across multiple PostgreSQL clusters in a SaaS platform.
- Scale event streams across multiple event stores without burdening domain logic.
- Implement tenant-based isolation by routing organizations to their assigned shards.
- Future-proof a growing CQRS/Event Sourcing system against database size limits.

---

## ⚙️ Extending Shardis

Shardis is designed for extension:

- **Custom Routing Strategies**
  Implement your own `IShardRouter` if you need consistent hashing rings, weighted shards, or region-aware routing.

- **Persistent Shard Maps**
  Replace the in-memory `IShardMapStore` with implementations backed by SQL, Redis, or cloud storage.

- **Shard Migrations and Rebalancing**
  Coming soon: native support for safely reassigning keys and migrating aggregates between shards.

---

## 🛡️ Design Philosophy

Shardis is built around three core principles:

1. **Determinism First**:
   Given the same ShardKey, the same shard must always be chosen unless explicitly migrated.

2. **Separation of Concerns**:
   Domain models should never "know" about shards — sharding remains purely an infrastructure concern.

3. **Minimal Intrusion**:
   Shardis integrates into your system without forcing heavy infrastructure or hosting requirements.

---

## 🚧 Roadmap

- [ ] Persistent ShardMapStore options (SQL, Redis)
- [ ] Shard migrator for safe rebalance operations
- [ ] Read/Write split support
- [ ] Multi-region / geo-sharding support
- [ ] Lightweight metrics/telemetry package

---

## 👨‍💻 Contributing

Pull requests, issues, and ideas are welcome.
If you find an interesting edge case or want to extend Shardis into more advanced scaling patterns, open a discussion or a PR!

See [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## 📄 License

**MIT License** — free for personal and commercial use.

---

> _"Because scaling your domain shouldn’t mean scaling your pain."_ 🚀

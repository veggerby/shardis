namespace Shardis.Migration.Marten.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using global::Marten;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Marten;
using Shardis.Migration.Model;
using Shardis.Model;

using Xunit;

public class MartenMigrationTests
{
    private const string EnvVar = "SHARDIS_TEST_PG"; // e.g. Host=localhost;Port=5432;Database=shardis_test;Username=postgres;Password=postgres

    // simple document model
    private sealed record TestDoc(string Id, string Name, int Value);

    // projection mutates Name -> upper
    private sealed class UpperNameProjectionStrategy : IEntityProjectionStrategy
    {
        public TTarget Project<TSource, TTarget>(TSource source, ProjectionContext context)
            where TSource : class where TTarget : class
        {
            if (source is TestDoc d && typeof(TTarget) == typeof(object))
            {
                return (TTarget)(object)new TestDoc(d.Id, d.Name.ToUpperInvariant(), d.Value);
            }
            return (TTarget)(object)source; // identity fallback
        }
    }

    private static string? Pg() => Environment.GetEnvironmentVariable(EnvVar);

    private static (ServiceProvider sp, IMartenSessionFactory factory, IShardDataMover<string> mover, IVerificationStrategy<string> verifier) Build(bool withProjection)
    {
        var conn = Pg();
        if (string.IsNullOrWhiteSpace(conn))
        {
            // Return a dummy provider so calling test can early-return.
            return (new ServiceCollection().BuildServiceProvider(), new NoOpFactory(), Substitute.For<IShardDataMover<string>>(), Substitute.For<IVerificationStrategy<string>>());
        }

        var services = new ServiceCollection();
        services.AddShardisMigration<string>(_ => { });
        services.AddMartenMigrationSupport<string>();
        if (withProjection)
        {
            services.AddSingleton<IEntityProjectionStrategy, UpperNameProjectionStrategy>();
        }
        services.AddSingleton<IMartenSessionFactory>(new TestMartenSessionFactory(conn));

        var sp = services.BuildServiceProvider();
        return (sp,
            sp.GetRequiredService<IMartenSessionFactory>(),
            sp.GetRequiredService<IShardDataMover<string>>(),
            sp.GetRequiredService<IVerificationStrategy<string>>());
    }

    // arrange helper: seed doc in source shard only
    private static async Task SeedAsync(IMartenSessionFactory factory, ShardId shard, TestDoc doc, CancellationToken ct = default)
    {
        await using var session = await factory.CreateDocumentSessionAsync(shard, ct);
        session.Store(doc);
        await session.SaveChangesAsync(ct);
    }

    private static KeyMove<string> Move(ShardId from, ShardId to, string key) => new(new ShardKey<string>(key), from, to);

    [Fact]
    public async Task Copy_Verify_Swap_Flow()
    {
        var (sp, factory, mover, verifier) = Build(withProjection: false);
        if (factory is NoOpFactory) { return; }
        var source = new ShardId("s1");
        var target = new ShardId("s2");
        var move = Move(source, target, "doc-1");

        await SeedAsync(factory, source, new TestDoc("doc-1", "alpha", 1));

        // act copy
        await mover.CopyAsync(move, CancellationToken.None);
        var verified = await verifier.VerifyAsync(move, CancellationToken.None);

        // assert
        verified.Should().BeTrue();
    }

    [Fact]
    public async Task Projection_Transform_Path()
    {
        var (sp, factory, mover, verifier) = Build(withProjection: true);
        if (factory is NoOpFactory) { return; }
        var source = new ShardId("s1");
        var target = new ShardId("s2");
        var move = Move(source, target, "doc-2");

        await SeedAsync(factory, source, new TestDoc("doc-2", "bravo", 7));

        await mover.CopyAsync(move, CancellationToken.None);
        var ok = await verifier.VerifyAsync(move, CancellationToken.None);

        ok.Should().BeTrue();

        // ensure projection applied (Name upper) in target
        await using var targetSession = await factory.CreateQuerySessionAsync(target);
        var loaded = await targetSession.LoadAsync<TestDoc>("doc-2");
        loaded!.Name.Should().Be("BRAVO");
    }

    [Fact]
    public async Task Idempotent_Rerun()
    {
        var (sp, factory, mover, verifier) = Build(withProjection: false);
        if (factory is NoOpFactory) { return; }
        var source = new ShardId("s1");
        var target = new ShardId("s2");
        var move = Move(source, target, "doc-3");

        await SeedAsync(factory, source, new TestDoc("doc-3", "alpha", 1));

        await mover.CopyAsync(move, CancellationToken.None);
        (await verifier.VerifyAsync(move, CancellationToken.None)).Should().BeTrue();

        // rerun copy (should be no errors and verification still true)
        await mover.CopyAsync(move, CancellationToken.None);
        (await verifier.VerifyAsync(move, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Checksum_Mismatch_Then_Recopy()
    {
        var (sp, factory, mover, verifier) = Build(withProjection: false);
        if (factory is NoOpFactory) { return; }
        var source = new ShardId("s1");
        var target = new ShardId("s2");
        var move = Move(source, target, "doc-4");

        await SeedAsync(factory, source, new TestDoc("doc-4", "init", 1));
        await mover.CopyAsync(move, CancellationToken.None);
        (await verifier.VerifyAsync(move, CancellationToken.None)).Should().BeTrue();

        // corrupt target
        await using (var corrupt = await factory.CreateDocumentSessionAsync(target))
        {
            corrupt.Store(new TestDoc("doc-4", "corrupt", 99));
            await corrupt.SaveChangesAsync();
        }

        (await verifier.VerifyAsync(move, CancellationToken.None)).Should().BeFalse();

        // recopy fixes
        await mover.CopyAsync(move, CancellationToken.None);
        (await verifier.VerifyAsync(move, CancellationToken.None)).Should().BeTrue();
    }

    private sealed class TestMartenSessionFactory(string connectionString) : IMartenSessionFactory
    {
        private readonly string _connectionString = connectionString;
        private readonly Dictionary<string, IDocumentStore> _stores = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        private IDocumentStore Get(ShardId shard)
        {
            if (_stores.TryGetValue(shard.Value, out var existing))
            {
                return existing;
            }
            lock (_gate)
            {
                if (_stores.TryGetValue(shard.Value, out existing))
                {
                    return existing;
                }
                var store = DocumentStore.For(opts =>
                {
                    opts.Connection(_connectionString);
                    opts.DatabaseSchemaName = $"mig_{shard.Value}"; // isolate per shard
                });
                _stores[shard.Value] = store;
                return store;
            }
        }

        public ValueTask<IQuerySession> CreateQuerySessionAsync(ShardId shardId, CancellationToken cancellationToken = default)
            => new(Get(shardId).QuerySession());

        public ValueTask<IDocumentSession> CreateDocumentSessionAsync(ShardId shardId, CancellationToken cancellationToken = default)
            => new(Get(shardId).LightweightSession());
    }

    private sealed class NoOpFactory : IMartenSessionFactory
    {
        public ValueTask<IQuerySession> CreateQuerySessionAsync(ShardId shardId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("No-op factory (environment not configured).");
        public ValueTask<IDocumentSession> CreateDocumentSessionAsync(ShardId shardId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("No-op factory (environment not configured).");
    }
}
using System.Data.Common;

using Marten;

using Npgsql;

using Xunit;

namespace Shardis.Marten.Tests;

/// <summary>
/// Spins up (or reuses) a local Postgres instance for Marten integration tests.
/// Relies on externally provided docker container to keep test deterministic & fast.
/// If POSTGRES_CONNECTION not set, tests using this fixture will be skipped.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public string? ConnectionString { get; private set; }
    public DocumentStore? Store { get; private set; }

    public Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return Task.CompletedTask; // will cause skip
        }
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(ConnectionString);
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Store?.Dispose();
        await Task.CompletedTask;
    }
}
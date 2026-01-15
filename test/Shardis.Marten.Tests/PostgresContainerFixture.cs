using Marten;

using Testcontainers.PostgreSql;

using Xunit;

namespace Shardis.Marten.Tests;

/// <summary>
/// xUnit fixture that manages a PostgreSQL container lifecycle for Marten integration tests.
/// Uses Testcontainers to automatically spin up and tear down a PostgreSQL instance.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("shardis_test")
            .WithUsername("test")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();
    public DocumentStore? Store { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(ConnectionString);
        });
    }

    public async Task DisposeAsync()
    {
        Store?.Dispose();
        await _container.DisposeAsync();
    }
}
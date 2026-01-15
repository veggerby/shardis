using Testcontainers.Redis;

namespace Shardis.Tests;

/// <summary>
/// xUnit fixture that manages a Redis container lifecycle for integration tests.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container;

    public RedisContainerFixture()
    {
        _container = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

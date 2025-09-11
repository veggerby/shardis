using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;

internal static class FailureMoverRegistration
{
    public static IServiceCollection DecorateIShardDataMoverWithFailureInjector(this IServiceCollection services)
    {
        // Find existing mover registration (last one wins for IShardDataMover<string>)
        // Replace with decorator that resolves original.
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IShardDataMover<string>));
        if (descriptor is null)
        {
            // Nothing to decorate; noop.
            return services;
        }
        services.Remove(descriptor);

        services.Add(new ServiceDescriptor(typeof(IShardDataMover<string>), sp =>
        {
            var original = (IShardDataMover<string>)(descriptor.ImplementationInstance
                ?? descriptor.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
            var decorator = new FailureInjectingMover(original);
            // Also expose decorator itself for direct injection.
            return decorator;
        }, descriptor.Lifetime));

        // Register the decorator concrete type for retrieval.
        services.AddSingleton(sp => (FailureInjectingMover)sp.GetRequiredService<IShardDataMover<string>>());

        return services;
    }
}
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nuntiator;
using Nuntiator.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up Nuntiator services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Nuntiator and scans the calling assembly for command handlers.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddNuntiator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddNuntiator(Assembly.GetCallingAssembly());
    }

    /// <summary>
    /// Registers Nuntiator and scans assemblies for command handlers and middlewares according to the provided configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddNuntiator(
        this IServiceCollection services,
        Action<NuntiatorConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new NuntiatorConfiguration(services);
        configure(config);

        services.TryAdd(new ServiceDescriptor(typeof(INuntiator), typeof(Nuntiator.Nuntiator), config.Lifetime));

        RegisterDiscoveredServices(services, config);

        return services;
    }

    /// <summary>
    /// Registers Nuntiator and scans the specified assemblies for command handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">Primary assembly to scan.</param>
    /// <param name="additionalAssemblies">Optional additional assemblies to scan.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddNuntiator(
        this IServiceCollection services,
        Assembly assembly,
        params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        return services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            if (additionalAssemblies != null && additionalAssemblies.Length > 0)
            {
                cfg.RegisterServicesFromAssemblies(additionalAssemblies);
            }
        });
    }

    /// <summary>
    /// Registers Nuntiator and scans the assemblies containing the specified marker types for command handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="markerType">Primary marker type whose containing assembly will be scanned.</param>
    /// <param name="additionalMarkerTypes">Optional additional marker types whose assemblies will be scanned.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddNuntiator(
        this IServiceCollection services,
        Type markerType,
        params Type[] additionalMarkerTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(markerType);

        return services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(markerType.Assembly);
            if (additionalMarkerTypes != null && additionalMarkerTypes.Length > 0)
            {
                foreach (var type in additionalMarkerTypes)
                {
                    cfg.RegisterServicesFromAssembly(type.Assembly);
                }
            }
        });
    }

    private static void RegisterDiscoveredServices(IServiceCollection services, NuntiatorConfiguration config)
    {
        foreach (var assembly in config.AssembliesToScan)
        {
            var types = GetExportedTypesSafely(assembly);

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                var interfaces = type.GetInterfaces();

                // 1. Check for ICommandHandler<TCommand, TResponse>
                foreach (var iface in interfaces)
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
                    {
                        services.Add(new ServiceDescriptor(iface, type, config.Lifetime));
                    }
                }

                // 2. Check for ICommandHandler<TCommand> (void)
                foreach (var iface in interfaces)
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICommandHandler<>))
                    {
                        var commandType = iface.GetGenericArguments()[0];
                        services.Add(new ServiceDescriptor(iface, type, config.Lifetime));

                        // Register adapter for ICommandHandler<TCommand, Unit> if not already implemented directly
                        var unitHandlerInterface = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(Unit));
                        if (!interfaces.Contains(unitHandlerInterface))
                        {
                            var adapterType = typeof(VoidCommandHandlerAdapter<>).MakeGenericType(commandType);
                            services.Add(new ServiceDescriptor(unitHandlerInterface, adapterType, config.Lifetime));
                        }
                    }
                }

                // 3. Optional auto-registration of middlewares
                if (config.AutoRegisterMiddlewares)
                {
                    foreach (var iface in interfaces)
                    {
                        if (iface.IsGenericType &&
                            (iface.GetGenericTypeDefinition() == typeof(ICommandMiddleware<,>) ||
                             iface.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)))
                        {
                            var commandType = iface.GetGenericArguments()[0];
                            var responseType = iface.GetGenericArguments()[1];
                            var middlewareInterface = typeof(ICommandMiddleware<,>).MakeGenericType(commandType, responseType);
                            services.Add(new ServiceDescriptor(middlewareInterface, type, config.Lifetime));
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<Type> GetExportedTypesSafely(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}

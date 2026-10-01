using System.Collections.Concurrent;
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

        if (config.ValidateOnStartup)
        {
            ValidateRegistrations(services, config);
        }

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

    private static void ValidateRegistrations(IServiceCollection services, NuntiatorConfiguration config)
    {
        var commandTypes = new List<(Type CommandType, Type ResponseType)>();

        foreach (var assembly in config.AssembliesToScan)
        {
            foreach (var type in GetExportedTypesSafely(assembly))
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICommand<>))
                    {
                        var responseType = iface.GetGenericArguments()[0];
                        commandTypes.Add((type, responseType));
                    }
                }
            }
        }

        if (commandTypes.Count == 0)
        {
            return;
        }

        var errors = new List<string>();

        foreach (var (commandType, responseType) in commandTypes.Distinct())
        {
            var handlerInterface = typeof(ICommandHandler<,>).MakeGenericType(commandType, responseType);
            var handlerDescriptors = services.Where(d => d.ServiceType == handlerInterface).ToArray();

            if (handlerDescriptors.Length == 0)
            {
                errors.Add($"No handler was found for command '{commandType.FullName}' (expected ICommandHandler<{commandType.Name}, {responseType.Name}>).");
            }
            else if (handlerDescriptors.Length > 1)
            {
                var handlerNames = string.Join(", ", handlerDescriptors.Select(d => $"'{DescribeImplementation(d)}'"));
                errors.Add($"Multiple handlers were found for command '{commandType.FullName}': {handlerNames}. Only one handler per command is allowed.");
            }
        }

        if (errors.Count > 0)
        {
            throw new NuntiatorConfigurationException(errors);
        }
    }

    private static string DescribeImplementation(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType != null)
        {
            return descriptor.ImplementationType.FullName ?? descriptor.ImplementationType.Name;
        }

        if (descriptor.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance.GetType().FullName ?? "instance";
        }

        return "factory-provided implementation";
    }

    private static readonly ConcurrentDictionary<Assembly, Type[]> ScannedTypesCache = new();

    private static Type[] GetExportedTypesSafely(Assembly assembly)
    {
        return ScannedTypesCache.GetOrAdd(assembly, static a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).ToArray()!;
            }
        });
    }
}

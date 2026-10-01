using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator;

/// <summary>
/// Configuration options for Nuntiator dependency injection registration.
/// </summary>
public class NuntiatorConfiguration
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Gets the list of assemblies to scan for command handlers and middlewares.
    /// </summary>
    public List<Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Gets or sets the service lifetime for handlers and middlewares. Default is <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets whether to automatically scan assemblies and register concrete middlewares.
    /// Default is <c>false</c> to encourage explicit and deterministic middleware ordering.
    /// </summary>
    public bool AutoRegisterMiddlewares { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to eagerly validate command handler registrations (missing or ambiguous handlers)
    /// when <c>AddNuntiator</c> is called, by building a temporary service provider. Default is <c>true</c>.
    /// Set to <c>false</c> to defer validation to the first failing call at runtime (previous behavior).
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;

    public NuntiatorConfiguration(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers handlers and optionally middlewares from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (!AssembliesToScan.Contains(assembly))
        {
            AssembliesToScan.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Registers handlers and optionally middlewares from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            RegisterServicesFromAssembly(assembly);
        }

        return this;
    }

    /// <summary>
    /// Registers handlers and optionally middlewares from the assembly containing <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">A type located in the target assembly.</typeparam>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration RegisterServicesFromAssemblyContaining<TMarker>()
    {
        return RegisterServicesFromAssembly(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Registers handlers and optionally middlewares from the assembly containing the specified marker type.
    /// </summary>
    /// <param name="markerType">A type located in the target assembly.</param>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration RegisterServicesFromAssemblyContaining(Type markerType)
    {
        ArgumentNullException.ThrowIfNull(markerType);
        return RegisterServicesFromAssembly(markerType.Assembly);
    }

    /// <summary>
    /// Registers a pipeline middleware (open-generic or closed-generic).
    /// </summary>
    /// <param name="middlewareType">The middleware type.</param>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration AddMiddleware(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);

        if (middlewareType.IsGenericTypeDefinition)
        {
            // Verify open-generic middleware implements ICommandMiddleware<,> or IPipelineBehavior<,>
            var hasMatchingInterface = middlewareType.GetInterfaces().Any(i =>
                i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(ICommandMiddleware<,>) ||
                 i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)));

            if (!hasMatchingInterface)
            {
                throw new ArgumentException(
                    $"Open generic type '{middlewareType.FullName}' must implement ICommandMiddleware<TCommand, TResponse> or IPipelineBehavior<TCommand, TResponse> (open generic arity 2). " +
                    $"Found interfaces: [{DescribeInterfaces(middlewareType)}]. " +
                    "If the type already implements one of these interfaces but with closed generic arguments, register it as a closed-generic middleware instead (e.g. AddMiddleware<TConcrete>()).",
                    nameof(middlewareType));
            }

            _services.Add(new ServiceDescriptor(typeof(ICommandMiddleware<,>), middlewareType, Lifetime));
        }
        else
        {
            var middlewareInterfaces = middlewareType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                           (i.GetGenericTypeDefinition() == typeof(ICommandMiddleware<,>) ||
                            i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)))
                .Select(i => i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)
                    ? typeof(ICommandMiddleware<,>).MakeGenericType(i.GetGenericArguments())
                    : i)
                .Distinct();

            bool registered = false;
            foreach (var iface in middlewareInterfaces)
            {
                _services.Add(new ServiceDescriptor(iface, middlewareType, Lifetime));
                registered = true;
            }

            if (!registered)
            {
                throw new ArgumentException(
                    $"Type '{middlewareType.FullName}' does not implement ICommandMiddleware<TCommand, TResponse> or IPipelineBehavior<TCommand, TResponse>. " +
                    $"Found interfaces: [{DescribeInterfaces(middlewareType)}]. " +
                    "Ensure the type implements one of these interfaces with concrete (closed) generic arguments matching the command/response types.",
                    nameof(middlewareType));
            }
        }

        return this;
    }

    private static string DescribeInterfaces(Type type)
    {
        var interfaceNames = type.GetInterfaces().Select(i => i.Name).ToArray();
        return interfaceNames.Length == 0 ? "none" : string.Join(", ", interfaceNames);
    }

    /// <summary>
    /// Registers a pipeline middleware of type <typeparamref name="TMiddleware"/>.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration AddMiddleware<TMiddleware>()
    {
        return AddMiddleware(typeof(TMiddleware));
    }

    /// <summary>
    /// Registers an open-generic pipeline middleware, e.g. <c>typeof(LoggingMiddleware&lt;,&gt;)</c>.
    /// </summary>
    /// <param name="openMiddlewareType">The open-generic middleware type.</param>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration AddOpenMiddleware(Type openMiddlewareType)
    {
        return AddMiddleware(openMiddlewareType);
    }

    /// <summary>
    /// MediatR-compatible alias for registering an open-generic pipeline behavior.
    /// </summary>
    /// <param name="openBehaviorType">The open-generic behavior type.</param>
    /// <returns>This configuration instance for fluent chaining.</returns>
    public NuntiatorConfiguration AddOpenBehavior(Type openBehaviorType)
    {
        return AddMiddleware(openBehaviorType);
    }
}

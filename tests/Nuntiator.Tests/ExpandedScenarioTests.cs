using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator.Tests;

public class ExpandedScenarioTests
{
    [Fact]
    public async Task DuplicateMiddlewareRegistrations_BothExecute_InRegistrationOrder()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.ValidateOnStartup = false;
            cfg.RegisterServicesFromAssembly(typeof(ExpandedScenarioTests).Assembly);
            cfg.AddMiddleware(typeof(FirstLoggingMiddleware<,>));
            cfg.AddMiddleware(typeof(FirstLoggingMiddleware<,>));
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        await nuntiator.SendAsync(new PingCommand("Dup"));

        // Registering the same open-generic middleware type twice results in two distinct middleware
        // instances in the pipeline (DI does not deduplicate registrations), each wrapping the call once.
        Assert.Equal(5, tracker.ExecutionSteps.Count);
        Assert.Equal("Middleware1:Before:PingCommand", tracker.ExecutionSteps[0]);
        Assert.Equal("Middleware1:Before:PingCommand", tracker.ExecutionSteps[1]);
        Assert.Equal("Handler:Ping:Dup", tracker.ExecutionSteps[2]);
        Assert.Equal("Middleware1:After:PingCommand", tracker.ExecutionSteps[3]);
        Assert.Equal("Middleware1:After:PingCommand", tracker.ExecutionSteps[4]);
    }

    [Fact]
    public void RegisterServicesFromAssemblies_WithMultipleAssemblies_ResolvesHandlersFromPrimaryAssembly()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(cfg =>
        {
            cfg.ValidateOnStartup = false;
            // Scans the test assembly plus an unrelated external assembly with no commands/handlers.
            cfg.RegisterServicesFromAssemblies(typeof(ExpandedScenarioTests).Assembly, typeof(ServiceCollection).Assembly);
        });

        var provider = services.BuildServiceProvider();

        var handler = provider.GetService<ICommandHandler<PingCommand, string>>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void ScopedLifetime_Middleware_ResolvesSameInstanceWithinScope_DifferentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(cfg =>
        {
            cfg.ValidateOnStartup = false;
            cfg.RegisterServicesFromAssembly(typeof(ExpandedScenarioTests).Assembly);
            cfg.Lifetime = ServiceLifetime.Scoped;
            cfg.AddMiddleware(typeof(FirstLoggingMiddleware<,>));
        });

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var mw1a = scope1.ServiceProvider.GetRequiredService<ICommandMiddleware<PingCommand, string>>();
        var mw1b = scope1.ServiceProvider.GetRequiredService<ICommandMiddleware<PingCommand, string>>();
        Assert.Same(mw1a, mw1b);

        using var scope2 = provider.CreateScope();
        var mw2 = scope2.ServiceProvider.GetRequiredService<ICommandMiddleware<PingCommand, string>>();
        Assert.NotSame(mw1a, mw2);
    }

    [Fact]
    public async Task SingletonLifetime_Handler_ReturnsSameInstanceAcrossResolutions()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(cfg =>
        {
            cfg.ValidateOnStartup = false;
            cfg.RegisterServicesFromAssembly(typeof(ExpandedScenarioTests).Assembly);
            cfg.Lifetime = ServiceLifetime.Singleton;
        });

        var provider = services.BuildServiceProvider();

        var handler1 = provider.GetRequiredService<ICommandHandler<PingCommand, string>>();
        var handler2 = provider.GetRequiredService<ICommandHandler<PingCommand, string>>();
        Assert.Same(handler1, handler2);

        var nuntiator = provider.GetRequiredService<INuntiator>();
        var result = await nuntiator.SendAsync(new PingCommand("Singleton"));
        Assert.Equal("Pong: Singleton", result);
    }

    [Fact]
    public async Task UnhandledException_WithNoCatchingMiddleware_PropagatesOriginalException()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.ValidateOnStartup = false;
            cfg.RegisterServicesFromAssembly(typeof(ExpandedScenarioTests).Assembly);
            // No ExceptionHandlingMiddleware registered, so the exception should propagate untouched.
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            nuntiator.SendAsync(new FailingCommand("Unhandled failure")));

        Assert.Equal("Unhandled failure", ex.Message);
    }

    [Fact]
    public void AddOpenMiddleware_WithInvalidType_ThrowsArgumentException_WithEnrichedMessage()
    {
        var services = new ServiceCollection();
        var config = new NuntiatorConfiguration(services);

        var ex = Assert.Throws<ArgumentException>(() => config.AddOpenMiddleware(typeof(List<>)));

        Assert.Contains(nameof(List<>), ex.Message, StringComparison.Ordinal);
        Assert.Contains("ICommandMiddleware", ex.Message, StringComparison.Ordinal);
        Assert.Contains("IPipelineBehavior", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiddleware_WithInvalidClosedType_ThrowsArgumentException_ListingFoundInterfaces()
    {
        var services = new ServiceCollection();
        var config = new NuntiatorConfiguration(services);

        var ex = Assert.Throws<ArgumentException>(() => config.AddMiddleware(typeof(string)));

        Assert.Contains("Found interfaces:", ex.Message, StringComparison.Ordinal);
    }
}

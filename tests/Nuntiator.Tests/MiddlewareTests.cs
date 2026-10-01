using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task OpenGenericMiddleware_WrapsExecutionBeforeAndAfter()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MiddlewareTests).Assembly);
            cfg.AddOpenMiddleware(typeof(FirstLoggingMiddleware<,>));
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        var result = await nuntiator.SendAsync(new PingCommand("Test"));

        Assert.Equal("Pong: Test", result);
        Assert.Equal(3, tracker.ExecutionSteps.Count);
        Assert.Equal("Middleware1:Before:PingCommand", tracker.ExecutionSteps[0]);
        Assert.Equal("Handler:Ping:Test", tracker.ExecutionSteps[1]);
        Assert.Equal("Middleware1:After:PingCommand", tracker.ExecutionSteps[2]);
    }

    [Fact]
    public async Task MultipleMiddlewares_ExecuteInDeterministicOrder()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MiddlewareTests).Assembly);
            cfg.AddMiddleware(typeof(FirstLoggingMiddleware<,>));
            cfg.AddMiddleware(typeof(SecondLoggingMiddleware<,>));
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        await nuntiator.SendAsync(new PingCommand("Pipeline"));

        Assert.Equal(5, tracker.ExecutionSteps.Count);
        Assert.Equal("Middleware1:Before:PingCommand", tracker.ExecutionSteps[0]);
        Assert.Equal("Middleware2:Before:PingCommand", tracker.ExecutionSteps[1]);
        Assert.Equal("Handler:Ping:Pipeline", tracker.ExecutionSteps[2]);
        Assert.Equal("Middleware2:After:PingCommand", tracker.ExecutionSteps[3]);
        Assert.Equal("Middleware1:After:PingCommand", tracker.ExecutionSteps[4]);
    }

    [Fact]
    public async Task Middlewares_InterceptVoidCommandsCorrectly()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MiddlewareTests).Assembly);
            cfg.AddMiddleware(typeof(FirstLoggingMiddleware<,>));
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        await nuntiator.SendAsync(new LogMessageCommand("VoidWithMiddleware"));

        Assert.Equal(3, tracker.ExecutionSteps.Count);
        Assert.Equal("Middleware1:Before:LogMessageCommand", tracker.ExecutionSteps[0]);
        Assert.Equal("Handler:Log:VoidWithMiddleware", tracker.ExecutionSteps[1]);
        Assert.Equal("Middleware1:After:LogMessageCommand", tracker.ExecutionSteps[2]);
    }

    [Fact]
    public async Task ClosedGenericMiddleware_ShortCircuitsExecution()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MiddlewareTests).Assembly);
            cfg.AddMiddleware<ShortCircuitingMiddleware>();
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        // When short-circuit is true:
        var shortCircuitedResult = await nuntiator.SendAsync(new ShortCircuitCommand(ShouldShortCircuit: true));
        Assert.Equal("ShortCircuitedResult", shortCircuitedResult);
        Assert.Contains("ShortCircuitMiddleware:Before", tracker.ExecutionSteps);
        Assert.Contains("ShortCircuitMiddleware:ShortCircuited", tracker.ExecutionSteps);
        Assert.DoesNotContain("Handler:ShortCircuitExecuted", tracker.ExecutionSteps);

        // When short-circuit is false:
        tracker.ExecutionSteps.Clear();
        var normalResult = await nuntiator.SendAsync(new ShortCircuitCommand(ShouldShortCircuit: false));
        Assert.Equal("HandlerExecuted", normalResult);
        Assert.Contains("ShortCircuitMiddleware:Before", tracker.ExecutionSteps);
        Assert.Contains("Handler:ShortCircuitExecuted", tracker.ExecutionSteps);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_CatchesAndHandlesException()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MiddlewareTests).Assembly);
            cfg.AddMiddleware(typeof(ExceptionHandlingMiddleware<,>));
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        var result = await nuntiator.SendAsync(new FailingCommand("Database connection failed"));

        Assert.Equal("HandledError: Database connection failed", result);
        Assert.Contains("CaughtException:Database connection failed", tracker.ExecutionSteps);
    }

    [Fact]
    public async Task PipelineBehavior_MediatRAlias_ExecutesProperly()
    {
        var tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MiddlewareTests).Assembly);
            cfg.AddOpenBehavior(typeof(SamplePipelineBehavior<,>));
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        var result = await nuntiator.SendAsync(new PingCommand("Behavior"));

        Assert.Equal("Pong: Behavior", result);
        Assert.Equal(3, tracker.ExecutionSteps.Count);
        Assert.Equal("PipelineBehavior:Before:PingCommand", tracker.ExecutionSteps[0]);
        Assert.Equal("Handler:Ping:Behavior", tracker.ExecutionSteps[1]);
        Assert.Equal("PipelineBehavior:After:PingCommand", tracker.ExecutionSteps[2]);
    }

    [Fact]
    public void AddMiddleware_WithInvalidType_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var config = new NuntiatorConfiguration(services);

        Assert.Throws<ArgumentException>(() => config.AddMiddleware(typeof(string)));
        Assert.Throws<ArgumentException>(() => config.AddMiddleware(typeof(List<>)));
    }
}

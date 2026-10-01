using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddNuntiator_WithMarkerType_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(typeof(DependencyInjectionTests));

        var provider = services.BuildServiceProvider();

        var nuntiator = provider.GetService<INuntiator>();
        Assert.NotNull(nuntiator);

        var handler = provider.GetService<ICommandHandler<PingCommand, string>>();
        Assert.NotNull(handler);

        var voidHandler = provider.GetService<ICommandHandler<LogMessageCommand>>();
        Assert.NotNull(voidHandler);

        var voidAdapter = provider.GetService<ICommandHandler<LogMessageCommand, Unit>>();
        Assert.NotNull(voidAdapter);
    }

    [Fact]
    public void AddNuntiator_WithAssembliesParam_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(typeof(DependencyInjectionTests).Assembly);

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetService<INuntiator>();

        Assert.NotNull(nuntiator);
    }

    [Fact]
    public void AddNuntiator_WithCallingAssembly_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator();

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetService<INuntiator>();

        Assert.NotNull(nuntiator);
    }

    [Fact]
    public async Task AddNuntiator_WithScopedLifetime_ResolvesScopedInstancesPerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjectionTests).Assembly);
            cfg.Lifetime = ServiceLifetime.Scoped;
        });

        var provider = services.BuildServiceProvider();

        using (var scope1 = provider.CreateScope())
        {
            var handler1a = scope1.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();
            var handler1b = scope1.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();
            Assert.Same(handler1a, handler1b);

            var nuntiator1 = scope1.ServiceProvider.GetRequiredService<INuntiator>();
            var result = await nuntiator1.SendAsync(new PingCommand("Scope1"));
            Assert.Equal("Pong: Scope1", result);
        }

        using (var scope2 = provider.CreateScope())
        {
            var handler2 = scope2.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();
            Assert.NotNull(handler2);
        }
    }

    [Fact]
    public void AddNuntiator_WithTransientLifetime_CreatesNewInstancesPerResolution()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutionTracker());
        services.AddNuntiator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjectionTests).Assembly);
            cfg.Lifetime = ServiceLifetime.Transient;
        });

        var provider = services.BuildServiceProvider();

        var handler1 = provider.GetRequiredService<ICommandHandler<PingCommand, string>>();
        var handler2 = provider.GetRequiredService<ICommandHandler<PingCommand, string>>();

        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void AddNuntiator_NullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddNuntiator(_ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddNuntiator(typeof(DependencyInjectionTests)));
    }
}

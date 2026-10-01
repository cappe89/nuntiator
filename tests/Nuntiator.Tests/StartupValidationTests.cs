using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator.Tests;

public class StartupValidationTests
{
    [Fact]
    public void MissingHandler_ThrowsNuntiatorConfigurationException_WhenValidateOnStartupIsTrue()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<NuntiatorConfigurationException>(() =>
        {
            services.AddNuntiator(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(UnhandledCommand).Assembly);
                // Intentionally do not register a handler for UnhandledCommand.
            });
        });

        Assert.Contains(ex.Errors, e => e.Contains(nameof(UnhandledCommand)));
    }

    [Fact]
    public void AmbiguousHandlers_ThrowsNuntiatorConfigurationException_ListingBothHandlers()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<NuntiatorConfigurationException>(() =>
        {
            services.AddNuntiator(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(AmbiguousCommand).Assembly);
            });
        });

        Assert.Contains(ex.Errors, e => e.Contains(nameof(AmbiguousCommand)) && e.Contains(nameof(AmbiguousCommandHandlerA)) && e.Contains(nameof(AmbiguousCommandHandlerB)));
    }

    [Fact]
    public async Task MissingHandler_DoesNotThrow_WhenValidateOnStartupIsFalse()
    {
        var services = new ServiceCollection();

        services.AddNuntiator(cfg =>
        {
            cfg.ValidateOnStartup = false;
            cfg.RegisterServicesFromAssembly(typeof(UnhandledCommand).Assembly);
        });

        var provider = services.BuildServiceProvider();
        var nuntiator = provider.GetRequiredService<INuntiator>();

        await Assert.ThrowsAsync<HandlerNotFoundException>(() => nuntiator.SendAsync(new UnhandledCommand("data")));
    }
}

// Dedicated command/handlers for the ambiguity test, kept separate from PingCommand so that other tests
// scanning the same assembly are not affected by having two registered handlers.
public record AmbiguousCommand(string Data) : ICommand<string>;

public class AmbiguousCommandHandlerA : ICommandHandler<AmbiguousCommand, string>
{
    public Task<string> HandleAsync(AmbiguousCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"A:{command.Data}");
    }
}

public class AmbiguousCommandHandlerB : ICommandHandler<AmbiguousCommand, string>
{
    public Task<string> HandleAsync(AmbiguousCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"B:{command.Data}");
    }
}

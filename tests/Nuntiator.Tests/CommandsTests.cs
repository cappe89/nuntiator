using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator.Tests;

public class CommandsTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ExecutionTracker _tracker;

    public CommandsTests()
    {
        _tracker = new ExecutionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(_tracker);
        services.AddNuntiator(cfg =>
        {
            // This fixture intentionally scans an assembly containing UnhandledCommand (no registered handler)
            // to exercise runtime HandlerNotFoundException behavior, so eager startup validation is disabled here.
            cfg.ValidateOnStartup = false;
            cfg.RegisterServicesFromAssembly(typeof(CommandsTests).Assembly);
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendAsync_CommandWithResponse_ReturnsExpectedResponse()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        var result = await nuntiator.SendAsync(new PingCommand("Hello"));

        Assert.Equal("Pong: Hello", result);
        Assert.Contains("Handler:Ping:Hello", _tracker.ExecutionSteps);
    }

    [Fact]
    public async Task Send_MediatRAlias_CommandWithResponse_ReturnsExpectedResponse()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        var result = await nuntiator.Send(new CalculateSumCommand(15, 27));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task SendAsync_VoidCommand_ExecutesHandlerSuccessfully()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        await nuntiator.SendAsync(new LogMessageCommand("Info message"));

        Assert.Contains("Handler:Log:Info message", _tracker.ExecutionSteps);
    }

    [Fact]
    public async Task Send_MediatRAlias_VoidCommand_ExecutesHandlerSuccessfully()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        await nuntiator.Send(new LogMessageCommand("Another message"));

        Assert.Contains("Handler:Log:Another message", _tracker.ExecutionSteps);
    }

    [Fact]
    public async Task SendAsync_UntypedCommandWithResponse_ReturnsResult()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        object command = new PingCommand("Untyped");

        var result = await nuntiator.SendAsync(command);

        Assert.Equal("Pong: Untyped", result);
    }

    [Fact]
    public async Task SendAsync_UntypedVoidCommand_ReturnsUnitValue()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        object command = new LogMessageCommand("Untyped Void");

        var result = await nuntiator.SendAsync(command);

        Assert.Equal(Unit.Value, result);
        Assert.Contains("Handler:Log:Untyped Void", _tracker.ExecutionSteps);
    }

    [Fact]
    public async Task SendAsync_NonCommandObject_ThrowsArgumentException()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        object invalidCommand = "Not A Command";

        await Assert.ThrowsAsync<ArgumentException>(() => nuntiator.SendAsync(invalidCommand));
    }

    [Fact]
    public async Task SendAsync_NullCommand_ThrowsArgumentNullException()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => nuntiator.SendAsync<string>((ICommand<string>)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => nuntiator.SendAsync((ICommand)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => nuntiator.SendAsync((object)null!));
    }

    [Fact]
    public async Task SendAsync_UnhandledCommand_ThrowsHandlerNotFoundException()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();

        var ex = await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            nuntiator.SendAsync(new UnhandledCommand("No handler for this")));

        Assert.Equal(typeof(UnhandledCommand), ex.CommandType);
        Assert.Equal(typeof(string), ex.ResponseType);
        Assert.Contains("UnhandledCommand", ex.Message);
    }

    [Fact]
    public async Task SendAsync_CancelledCancellationToken_ThrowsOperationCanceledException()
    {
        var nuntiator = _serviceProvider.GetRequiredService<INuntiator>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            nuntiator.SendAsync(new PingCommand("CancelMe"), cts.Token));
    }
}

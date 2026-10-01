namespace Nuntiator.Tests;

// Commands
public record PingCommand(string Message) : ICommand<string>;

public record CalculateSumCommand(int A, int B) : ICommand<int>;

public record LogMessageCommand(string Message) : ICommand;

public record UnhandledCommand(string Data) : ICommand<string>;

public record ShortCircuitCommand(bool ShouldShortCircuit) : ICommand<string>;

public record FailingCommand(string ErrorMessage) : ICommand<string>;

// State tracker for verifying execution order & side-effects
public class ExecutionTracker
{
    public List<string> ExecutionSteps { get; } = new();
}

// Handlers
public class PingCommandHandler(ExecutionTracker tracker) : ICommandHandler<PingCommand, string>
{
    public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tracker.ExecutionSteps.Add($"Handler:Ping:{command.Message}");
        return Task.FromResult($"Pong: {command.Message}");
    }
}

public class CalculateSumCommandHandler : ICommandHandler<CalculateSumCommand, int>
{
    public Task<int> HandleAsync(CalculateSumCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(command.A + command.B);
    }
}

public class LogMessageCommandHandler(ExecutionTracker tracker) : ICommandHandler<LogMessageCommand>
{
    public Task HandleAsync(LogMessageCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tracker.ExecutionSteps.Add($"Handler:Log:{command.Message}");
        return Task.CompletedTask;
    }
}

public class ShortCircuitCommandHandler(ExecutionTracker tracker) : ICommandHandler<ShortCircuitCommand, string>
{
    public Task<string> HandleAsync(ShortCircuitCommand command, CancellationToken cancellationToken = default)
    {
        tracker.ExecutionSteps.Add("Handler:ShortCircuitExecuted");
        return Task.FromResult("HandlerExecuted");
    }
}

public class FailingCommandHandler : ICommandHandler<FailingCommand, string>
{
    public Task<string> HandleAsync(FailingCommand command, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(command.ErrorMessage);
    }
}

// Middlewares
public class FirstLoggingMiddleware<TCommand, TResponse>(ExecutionTracker tracker)
    : ICommandMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tracker.ExecutionSteps.Add($"Middleware1:Before:{typeof(TCommand).Name}");
        var response = await next();
        tracker.ExecutionSteps.Add($"Middleware1:After:{typeof(TCommand).Name}");
        return response;
    }
}

public class SecondLoggingMiddleware<TCommand, TResponse>(ExecutionTracker tracker)
    : ICommandMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tracker.ExecutionSteps.Add($"Middleware2:Before:{typeof(TCommand).Name}");
        var response = await next();
        tracker.ExecutionSteps.Add($"Middleware2:After:{typeof(TCommand).Name}");
        return response;
    }
}

public class ShortCircuitingMiddleware(ExecutionTracker tracker)
    : ICommandMiddleware<ShortCircuitCommand, string>
{
    public async Task<string> HandleAsync(
        ShortCircuitCommand command,
        CommandHandlerDelegate<string> next,
        CancellationToken cancellationToken = default)
    {
        tracker.ExecutionSteps.Add("ShortCircuitMiddleware:Before");
        if (command.ShouldShortCircuit)
        {
            tracker.ExecutionSteps.Add("ShortCircuitMiddleware:ShortCircuited");
            return "ShortCircuitedResult";
        }

        return await next();
    }
}

public class ExceptionHandlingMiddleware<TCommand, TResponse>(ExecutionTracker tracker)
    : ICommandMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await next();
        }
        catch (InvalidOperationException ex)
        {
            tracker.ExecutionSteps.Add($"CaughtException:{ex.Message}");
            if (typeof(TResponse) == typeof(string))
            {
                return (TResponse)(object)$"HandledError: {ex.Message}";
            }
            throw;
        }
    }
}

public class SamplePipelineBehavior<TCommand, TResponse>(ExecutionTracker tracker)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        tracker.ExecutionSteps.Add($"PipelineBehavior:Before:{typeof(TCommand).Name}");
        var result = await next();
        tracker.ExecutionSteps.Add($"PipelineBehavior:After:{typeof(TCommand).Name}");
        return result;
    }
}

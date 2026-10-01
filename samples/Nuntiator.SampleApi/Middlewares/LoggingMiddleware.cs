using System.Diagnostics;
using Nuntiator;

namespace Nuntiator.SampleApi.Middlewares;

public class LoggingMiddleware<TCommand, TResponse> : ICommandMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly ILogger<LoggingMiddleware<TCommand, TResponse>> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware<TCommand, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var commandName = typeof(TCommand).Name;
        _logger.LogInformation("--> [Nuntiator Pipeline] Starting execution of {CommandName}", commandName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            _logger.LogInformation("<-- [Nuntiator Pipeline] Completed {CommandName} in {ElapsedMs}ms", commandName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "<-- [Nuntiator Pipeline] Failed {CommandName} after {ElapsedMs}ms", commandName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

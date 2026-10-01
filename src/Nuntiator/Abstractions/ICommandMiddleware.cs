namespace Nuntiator;

/// <summary>
/// Delegate representing the next handler or middleware in the execution pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the pipeline.</typeparam>
/// <returns>A task that represents the asynchronous operation, yielding the response.</returns>
public delegate Task<TResponse> CommandHandlerDelegate<TResponse>();

/// <summary>
/// Middleware that wraps around the execution of a command handler.
/// Allows performing actions before/after execution, modifying results, or short-circuiting.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommandMiddleware<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Intercepts command handling before and after the handler or next middleware in the pipeline.
    /// </summary>
    /// <param name="command">The command being handled.</param>
    /// <param name="next">Delegate to invoke the next middleware or final handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task yielding the result of the command handling.</returns>
    Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}

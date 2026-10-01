namespace Nuntiator;

/// <summary>
/// Dispatches commands to their respective handlers through any configured pipeline middlewares.
/// </summary>
public interface INuntiator
{
    /// <summary>
    /// Dispatches a command that produces a response to its registered handler through any configured middlewares.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the command execution.</returns>
    Task<TResponse> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a void command to its registered handler through any configured middlewares.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendAsync(
        ICommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a command whose response type may not be known at compile time.
    /// </summary>
    /// <param name="command">The command instance (must implement <see cref="ICommand{TResponse}"/> or <see cref="ICommand"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the command execution, or <see cref="Unit.Value"/> for void commands.</returns>
    Task<object?> SendAsync(
        object command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MediatR-compatible alias for <see cref="SendAsync{TResponse}(ICommand{TResponse}, CancellationToken)"/>.
    /// </summary>
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default) => SendAsync(command, cancellationToken);

    /// <summary>
    /// MediatR-compatible alias for <see cref="SendAsync(ICommand, CancellationToken)"/>.
    /// </summary>
    Task Send(
        ICommand command,
        CancellationToken cancellationToken = default) => SendAsync(command, cancellationToken);
}

namespace Nuntiator;

/// <summary>
/// Pipeline behavior interface, provided as a MediatR-compatible alias for <see cref="ICommandMiddleware{TCommand, TResponse}"/>.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TCommand, TResponse> : ICommandMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}

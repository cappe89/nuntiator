namespace Nuntiator.Internal;

/// <summary>
/// Adapts an <see cref="ICommandHandler{TCommand}"/> (void) to an <see cref="ICommandHandler{TCommand, Unit}"/>
/// so that void commands flow through the uniform pipeline behavior infrastructure.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public sealed class VoidCommandHandlerAdapter<TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _innerHandler;

    public VoidCommandHandlerAdapter(ICommandHandler<TCommand> innerHandler)
    {
        _innerHandler = innerHandler ?? throw new ArgumentNullException(nameof(innerHandler));
    }

    public async Task<Unit> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        await _innerHandler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

using Nuntiator.Internal;

namespace Nuntiator;

/// <summary>
/// Default implementation of <see cref="INuntiator"/> dispatcher.
/// </summary>
public sealed class Nuntiator : INuntiator
{
    private readonly IServiceProvider _serviceProvider;

    public Nuntiator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoker = PipelineInvokerCache.GetInvoker<TResponse>(command.GetType());
        return invoker.InvokeAsync(_serviceProvider, command, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendAsync(
        ICommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await SendAsync<Unit>(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<object?> SendAsync(
        object command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();
        var commandInterface = commandType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

        if (commandInterface == null)
        {
            throw new ArgumentException(
                $"Type '{commandType.FullName}' does not implement ICommand<TResponse> or ICommand.",
                nameof(command));
        }

        var responseType = commandInterface.GetGenericArguments()[0];
        var invoker = PipelineInvokerCache.GetUntypedInvoker(commandType, responseType);
        return invoker.InvokeUntypedAsync(_serviceProvider, command, cancellationToken);
    }
}

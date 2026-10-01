using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Nuntiator.Internal;

internal interface IPipelineInvokerUntyped
{
    Task<object?> InvokeUntypedAsync(IServiceProvider serviceProvider, object command, CancellationToken cancellationToken);
}

internal interface IPipelineInvoker<TResponse> : IPipelineInvokerUntyped
{
    Task<TResponse> InvokeAsync(IServiceProvider serviceProvider, object command, CancellationToken cancellationToken);
}

internal sealed class PipelineInvoker<TCommand, TResponse> : IPipelineInvoker<TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> InvokeAsync(IServiceProvider serviceProvider, object command, CancellationToken cancellationToken)
    {
        var typedCommand = (TCommand)command;
        var handlers = serviceProvider.GetServices<ICommandHandler<TCommand, TResponse>>().ToArray();

        if (handlers.Length == 0)
        {
            throw new HandlerNotFoundException(typeof(TCommand), typeof(TResponse));
        }

        if (handlers.Length > 1)
        {
            throw new AmbiguousHandlerException(
                typeof(TCommand),
                typeof(TResponse),
                handlers.Select(h => h.GetType()).ToArray());
        }

        var handler = handlers[0];

        var middlewares = serviceProvider.GetServices<ICommandMiddleware<TCommand, TResponse>>();

        CommandHandlerDelegate<TResponse> next = () => handler.HandleAsync(typedCommand, cancellationToken);

        foreach (var middleware in middlewares.Reverse())
        {
            var currentNext = next;
            var currentMiddleware = middleware;
            next = () => currentMiddleware.HandleAsync(typedCommand, currentNext, cancellationToken);
        }

        return await next().ConfigureAwait(false);
    }

    public async Task<object?> InvokeUntypedAsync(IServiceProvider serviceProvider, object command, CancellationToken cancellationToken)
    {
        var result = await InvokeAsync(serviceProvider, command, cancellationToken).ConfigureAwait(false);
        return result;
    }
}

internal static class PipelineInvokerCache
{
    private static readonly ConcurrentDictionary<(Type CommandType, Type ResponseType), object> Cache = new();

    public static IPipelineInvoker<TResponse> GetInvoker<TResponse>(Type commandType)
    {
        var key = (commandType, typeof(TResponse));
        return (IPipelineInvoker<TResponse>)Cache.GetOrAdd(key, static k =>
        {
            var invokerType = typeof(PipelineInvoker<,>).MakeGenericType(k.CommandType, k.ResponseType);
            return Activator.CreateInstance(invokerType)!;
        });
    }

    public static IPipelineInvokerUntyped GetUntypedInvoker(Type commandType, Type responseType)
    {
        var key = (commandType, responseType);
        return (IPipelineInvokerUntyped)Cache.GetOrAdd(key, static k =>
        {
            var invokerType = typeof(PipelineInvoker<,>).MakeGenericType(k.CommandType, k.ResponseType);
            return Activator.CreateInstance(invokerType)!;
        });
    }
}

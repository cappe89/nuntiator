namespace Nuntiator;

/// <summary>
/// Exception thrown when more than one handler is registered for the same command type.
/// </summary>
public class AmbiguousHandlerException : NuntiatorException
{
    /// <summary>
    /// The command type for which multiple handlers were found.
    /// </summary>
    public Type CommandType { get; }

    /// <summary>
    /// The expected response type, or <c>null</c> if not specified.
    /// </summary>
    public Type? ResponseType { get; }

    /// <summary>
    /// The concrete handler types that are in conflict.
    /// </summary>
    public IReadOnlyList<Type> HandlerTypes { get; }

    public AmbiguousHandlerException(Type commandType, Type? responseType, IReadOnlyList<Type> handlerTypes)
        : base(FormatMessage(commandType, responseType, handlerTypes))
    {
        CommandType = commandType;
        ResponseType = responseType;
        HandlerTypes = handlerTypes;
    }

    private static string FormatMessage(Type commandType, Type? responseType, IReadOnlyList<Type> handlerTypes)
    {
        var handlerNames = string.Join(", ", handlerTypes.Select(t => $"'{t.FullName}'"));

        if (responseType != null && responseType != typeof(Unit))
        {
            return $"Multiple handlers of type ICommandHandler<{commandType.Name}, {responseType.Name}> were found for command '{commandType.FullName}': {handlerNames}. Only one handler per command is allowed.";
        }

        return $"Multiple handlers were found for command '{commandType.FullName}': {handlerNames}. Only one handler per command is allowed.";
    }
}

namespace Nuntiator;

/// <summary>
/// Exception thrown when no handler is registered for a given command type.
/// </summary>
public class HandlerNotFoundException : NuntiatorException
{
    /// <summary>
    /// The command type for which no handler was found.
    /// </summary>
    public Type CommandType { get; }

    /// <summary>
    /// The expected response type, or <c>null</c> if not specified.
    /// </summary>
    public Type? ResponseType { get; }

    public HandlerNotFoundException(Type commandType, Type? responseType = null)
        : base(FormatMessage(commandType, responseType))
    {
        CommandType = commandType;
        ResponseType = responseType;
    }

    private static string FormatMessage(Type commandType, Type? responseType)
    {
        if (responseType != null && responseType != typeof(Unit))
        {
            return $"No handler of type ICommandHandler<{commandType.Name}, {responseType.Name}> was found for command '{commandType.FullName}'.";
        }

        return $"No handler was found for command '{commandType.FullName}'.";
    }
}

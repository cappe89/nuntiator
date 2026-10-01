namespace Nuntiator;

/// <summary>
/// Represents a command that yields a response when handled.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by handling this command.</typeparam>
public interface ICommand<out TResponse>
{
}

/// <summary>
/// Represents a command that does not produce a response (void).
/// </summary>
public interface ICommand : ICommand<Unit>
{
}

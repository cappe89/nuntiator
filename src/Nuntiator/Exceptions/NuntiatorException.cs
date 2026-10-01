namespace Nuntiator;

/// <summary>
/// Base exception for Nuntiator-related failures.
/// </summary>
public class NuntiatorException : Exception
{
    public NuntiatorException(string message) : base(message)
    {
    }

    public NuntiatorException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

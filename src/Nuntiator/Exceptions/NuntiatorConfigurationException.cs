namespace Nuntiator;

/// <summary>
/// Exception thrown when startup validation (<see cref="NuntiatorConfiguration.ValidateOnStartup"/>) detects one or more
/// configuration problems, such as missing or ambiguous command handlers.
/// </summary>
public class NuntiatorConfigurationException : NuntiatorException
{
    /// <summary>
    /// The individual validation errors detected during startup.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public NuntiatorConfigurationException(IReadOnlyList<string> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IReadOnlyList<string> errors)
    {
        return $"Nuntiator startup validation failed with {errors.Count} error(s):{Environment.NewLine}" +
               string.Join(Environment.NewLine, errors.Select(e => $"- {e}"));
    }
}

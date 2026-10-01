namespace Nuntiator;

/// <summary>
/// Represents a void type, since <see cref="System.Void"/> is not a valid return type in C# generics.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    private static readonly Unit _value = default;

    /// <summary>
    /// Default and only <see cref="Unit"/> value.
    /// </summary>
    public static ref readonly Unit Value => ref _value;

    /// <summary>
    /// Completed task containing the <see cref="Unit"/> value.
    /// </summary>
    public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(_value);

    /// <summary>
    /// Completed value task containing the <see cref="Unit"/> value.
    /// </summary>
    public static ValueTask<Unit> ValueTask { get; } = System.Threading.Tasks.ValueTask.FromResult(_value);

    public int CompareTo(Unit other) => 0;

    int IComparable.CompareTo(object? obj) => obj switch
    {
        null => 1,
        Unit => 0,
        _ => throw new ArgumentException($"Object must be of type {nameof(Unit)}", nameof(obj))
    };

    public override int GetHashCode() => 0;

    public bool Equals(Unit other) => true;

    public override bool Equals(object? obj) => obj is Unit;

    public static bool operator ==(Unit left, Unit right) => true;

    public static bool operator !=(Unit left, Unit right) => false;

    public override string ToString() => "()";
}

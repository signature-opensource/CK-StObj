namespace CK.Setup;

/// <summary>
/// Start parameter marker: the context object exists but its main handler methods have not been called yet.
/// <para>
/// Only the engine creates it: a handler method that takes it is called before the main methods of the context (the ones that take the context itself).
/// </para>
/// </summary>
/// <typeparam name="T">The context type.</typeparam>
public readonly struct Starting<T> where T : class
{
    internal Starting( T value ) => Value = value;

    /// <summary>
    /// Gets the context object that is starting.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Overridden to return "Starting&lt;<typeparamref name="T"/>&gt;: <see cref="Value"/>".
    /// </summary>
    public override string ToString() => $"Starting<{typeof( T ).Name}>: {Value}";
}

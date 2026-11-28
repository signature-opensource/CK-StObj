namespace CK.Setup;

/// <summary>
/// End parameter marker: the context object has successfully been handled.
/// <para>
/// Only the engine creates it: a handler method that takes it is called after the main methods of the context (and its inner contexts) when no error occurred (otherwise <see cref="OnError{T}"/> is called).
/// </para>
/// </summary>
/// <typeparam name="T">The context type.</typeparam>
public readonly struct Ending<T> where T : class
{
    internal Ending( T value ) => Value = value;

    /// <summary>
    /// Gets the context object that is ending.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Overridden to return "Ending&lt;<typeparamref name="T"/>&gt;: <see cref="Value"/>".
    /// </summary>
    public override string ToString() => $"Ending<{typeof( T ).Name}>: {Value}";
}

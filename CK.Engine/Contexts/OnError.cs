namespace CK.Setup;

/// <summary>
/// Error parameter marker: an error occurred while handling the context object.
/// <para>
/// Only the engine creates it: a handler method that takes it is called instead of the <see cref="Ending{T}"/> methods when an error occurred in the context (or in one of its inner contexts): it may be called even if the other methods of the handler have not been called.
/// </para>
/// </summary>
/// <typeparam name="T">The context type.</typeparam>
public readonly struct OnError<T> where T : class
{
    internal OnError( T value ) => Value = value;

    /// <summary>
    /// Gets the context object that failed.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Overridden to return "OnError&lt;<typeparamref name="T"/>&gt;: <see cref="Value"/>".
    /// </summary>
    public override string ToString() => $"OnError<{typeof( T ).Name}>: {Value}";
}

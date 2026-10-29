namespace CK.Core;

/// <summary>
/// Interface for IPoco class generated implementation.
/// All poco final implementation implement this interface that gives access to its <see cref="IPocoFactory"/>.
/// The implementation is explicit so that this doesn't appear in the public properties of the final Type when
/// using reflection.
/// </summary>
public interface IPocoGeneratedClass
{
    /// <summary>
    /// Gets the poco factory.
    /// </summary>
    IPocoFactory Factory { get; }

    /// <summary>
    /// Creates a deep copy of this Poco: nested Pocos, collections and records are copied, immutable values are shared.
    /// The copy is a tree: references shared by this Poco are not shared in the copy and a cycle is not supported.
    /// <para>
    /// The <see cref="PocoExtension.Clone{T}(T)"/> extension method is easier to use.
    /// </para>
    /// </summary>
    /// <returns>A new Poco of the same type.</returns>
    IPoco Clone();
}

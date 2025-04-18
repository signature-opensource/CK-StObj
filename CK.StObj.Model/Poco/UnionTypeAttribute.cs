using System;

namespace CK.Core;

/// <summary>
/// Defines multiples allowed types on a Poco property thanks to the type of a value type.
/// This attribute can be applied only on IPoco properties.
/// <para>
/// The allowed types are defined thanks to the local UnionTypes class
/// that exposes one public value tuple property for each union type.
/// <code>
///     public interface IPocoWithUnionType : IPoco
///     {
///         [UnionType]
///         object? Thing { get; set; }
/// 
///         [UnionType]
///         object AnotherThing { get; set; }
/// 
///         class UnionTypes
///         {
///             public (int, string, List&lt;string&gt;) Thing { get; }
///             public (ushort, List&lt;string?&gt;) AnotherThing { get; }
///         }
///     }
/// </code>
/// </para>
/// </summary>
/// <remarks>
/// Note that this not-so-funny UnionTypes class and its properties are the only way to capture the
/// types with their Nullable Reference Type information since the typeof operator doesn't capture
/// any NRT information.
/// <para>
/// Class (rather that struct) and public properties have been chosen (against nicer/shorter private fields) in order
/// to avoid any warnings (unused fields).
/// </para>
/// </remarks>
[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
public class UnionTypeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether this union type can be extended with other types by other <see cref="IPoco"/> interfaces
    /// of the same Poco.
    /// Defaults to false: types can only be the ones that are defined here.
    /// </summary>
    public bool CanBeExtended { get; set; }
}

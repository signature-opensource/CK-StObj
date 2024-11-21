using System.Diagnostics.CodeAnalysis;

namespace CK.Setup;

/// <summary>
/// Describes if and how a type can belong to a <see cref="ICompositePocoType"/>.
/// <para>
/// One and only one among <see cref="IsDisallowed"/>, <see cref="IsAllowed"/> and <see cref="RequiresInit"/>
/// is true.
/// </para>
/// </summary>
public readonly struct DefaultValueInfo
{
    /// <summary>
    /// Disallowed info (also the <c>default</c> of this type).
    /// </summary>
    public static DefaultValueInfo Disallowed => default;

    /// <summary>
    /// Allowed info has a null <see cref="DefaultValue"/>. The <c>default</c> of
    /// the type is applicable.
    /// </summary>
    public static DefaultValueInfo Allowed => new DefaultValueInfo( null );

    readonly IPocoFieldDefaultValue? _def;
    readonly bool _requiresInit;
    readonly bool _allowed;

    /// <summary>
    /// Initialize an Allowed or RequiresInit info.
    /// </summary>
    /// <param name="defaultValue">The default value. When null, <see cref="Allowed"/> is true.</param>
    public DefaultValueInfo( IPocoFieldDefaultValue? defaultValue )
    {
        if( (_def = defaultValue) != null )
        {
            _requiresInit = true;
            _allowed = false;
        }
        else
        {
            _requiresInit = false;
            _allowed = true;
        }
    }

    /// <summary>
    /// When true, the type can only be used externally, as a field or property of
    /// a <see cref="IRecordPocoType"/> that appear in a collection.
    /// <para>
    /// The type cannot be instantiated without violating its constraint.
    /// For instance, in the value tuple <c>(IPoco? A, IPoco B)</c>, B cannot
    /// be null and cannot be resolved to a non null instance: this tuple is
    /// initially invalid.
    /// </para>
    /// </summary>
    public bool IsDisallowed => !RequiresInit && !IsAllowed;

    /// <summary>
    /// The type can be used as a <see cref="IPrimaryPocoField"/> or <see cref="IRecordPocoType"/>
    /// field without any initialization: the .NET <c>default</c> value of the type does the job.
    /// <para>
    /// All nullable types are "Allowed", they will be initialized to null.
    /// </para>
    /// </summary>
    public bool IsAllowed => _allowed;

    /// <summary>
    /// The type can be used as a <see cref="IPrimaryPocoField"/> or <see cref="IRecordPocoType"/>
    /// field. The type is not nullable and requires an initialization: the <see cref="DefaultValue"/>
    /// must be used.
    /// </summary>
    [MemberNotNullWhen( true, nameof( DefaultValue ) )]
    public bool RequiresInit => _requiresInit;

    /// <summary>
    /// Gets the default value if a <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined
    /// or if a positional parameter of a record struct has a default value or if it can be automatically
    /// synthesized.
    /// <para>
    /// For IPoco, when the [DefaultValue] is defined on a field by more than one IPoco interface, it must be the same.
    /// </para>
    /// </summary>
    public IPocoFieldDefaultValue? DefaultValue => _def;

    /// <summary>
    /// Overridden to return the default value.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString()
    {
        if( RequiresInit ) return $"(Default: '{DefaultValue.ValueCSharpSource}')";
        if( _allowed ) return "(Allowed)";
        return "(Disallowed)";
    }
}

using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Common field attributes for <see cref="IPrimaryPocoField"/> and <see cref="IRecordPocoField"/>.
    /// This is a <see cref="IPocoType.ITypeRef"/>: every field has an <see cref="IPocoType.ITypeRef.Index"/>
    /// in its <see cref="Owner"/>.
    /// </summary>
    public interface IPocoField : IPocoType.ITypeRef
    {
        /// <summary>
        /// Gets the owner of this field.
        /// </summary>
        new ICompositePocoType Owner { get; }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the reflection object that defines this field. It can be a <see cref="PropertyInfo"/>, a <see cref="FieldInfo"/>
        /// a <see cref="IPocoPropertyInfo"/> or a <see cref="ParameterInfo"/> for record struct with constructor parameters.
        /// <para>
        /// It is null for value tuple fields (anonymous records).
        /// </para>
        /// </summary>
        object? Originator { get; }

        /// <summary>
        /// Gets whether this field is disallowed in a owner, always allowed or
        /// allowed but requires the <see cref="DefaultValueInfo.DefaultValue"/> to be set.
        /// </summary>
        DefaultValueInfo DefaultValueInfo { get; }

        /// <summary>
        /// Gets whether this field's <see cref="DefaultValueInfo"/> is not disallowed (either it requires
        /// an initialization or its default value is fine) and is specific to this field: it is not the
        /// same as the <see cref="IPocoType.DefaultValueInfo"/>.
        /// </summary>
        bool HasOwnDefaultValue { get; }

    }
}

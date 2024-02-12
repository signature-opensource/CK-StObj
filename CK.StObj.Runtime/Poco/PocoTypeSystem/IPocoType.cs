using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Poco type information.
    /// </summary>
    public interface IPocoType : IAnnotationSet
    {
        /// <summary>
        /// Compact index that uniquely identifies this type
        /// in the <see cref="IPocoTypeSystemBuilder.AllTypes"/> list.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the Type. When this is a value type and <see cref="IsNullable"/> is true,
        /// this is a <see cref="Nullable{T}"/>.
        /// <para>
        /// This is the <see cref="IDynamicAssembly.PurelyGeneratedType"/> marker type if <see cref="IsPurelyGeneratedType"/> is true.
        /// </para>
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets whether the <see cref="Type"/> is a purely generated type.
        /// When true, the Type property is <see cref="IDynamicAssembly.PurelyGeneratedType"/>.
        /// </summary>
        bool IsPurelyGeneratedType { get; }

        /// <summary>
        /// Gets this type's kind.
        /// </summary>
        PocoTypeKind Kind { get; }

        /// <summary>
        /// Gets whether this type has no available implementation in the type system.
        /// <list type="bullet">
        ///     <item>This starts with <see cref="IAbstractPocoType"/> that have no <see cref="IAbstractPocoType.PrimaryPocoTypes"/>.</item>
        ///     <item>Collections that references any implementation less types are also implementation less.</item>
        ///     <item>Generic <see cref="IAbstractPocoType"/> that have any implementation less <see cref="IAbstractPocoType.GenericArguments"/> type are also implementation less.</item>
        ///     <item><see cref="IUnionPocoType"/> that have all their <see cref="IOneOfPocoType.AllowedTypes"/> implementation less are also implementation less.</item>
        /// </list>
        /// <para>
        /// Implementation less types exist on the C# side and then are modelized but are unused extension points.
        /// </para>
        /// </summary>
        bool ImplementationLess { get; }

        /// <summary>
        /// Gets whether this type is disallowed as a field in a <see cref="ICompositePocoType"/>,
        /// or always allowed, or allowed but requires the <see cref="DefaultValueInfo.DefaultValue"/> to be set.
        /// <para>
        /// Note that a <see cref="DefaultValueInfo.Disallowed"/> type may perfectly be used in a composite type
        /// if and only if a default value specified at the field level can be resolved.
        /// </para>
        /// </summary>
        DefaultValueInfo DefaultValueInfo { get; }

        /// <summary>
        /// Gets whether this type is nullable.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Gets whether this type oblivious. See <see cref="ObliviousType"/>.
        /// </summary>
        bool IsOblivious => ObliviousType == this;

        /// <summary>
        /// Gets the C# name with namespaces and nullabilities of this type.
        /// </summary>
        string CSharpName { get; }

        /// <summary>
        /// Gets the implementation C# type name for this type.
        /// </summary>
        string ImplTypeName { get; }

        /// <summary>
        /// Gets the oblivious type (this instance if <see cref="IsOblivious"/> is true).
        /// <list type="bullet">
        ///   <item>
        ///     <term>Nullable Reference Types</term>
        ///     <description>
        ///         Oblivious type of a reference type is always the <see cref="NonNullable"/>.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <term>Collection types</term>
        ///     <description>
        ///         Abstract collections (readonly or not) are mapped to their regular <see cref="List{T}"/>,
        ///         <see cref="HashSet{T}"/> and <see cref="Dictionary{TKey, TValue}"/> where generic arguments
        ///         are oblivious.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <term>Value Tuple (Anonymous records)</term>
        ///     <description>
        ///         The oblivious type is the value tuple with no field names (all <see cref="IRecordPocoField.IsUnnamed"/>
        ///         are true) and with references to oblivious types (all field's types are oblivious).
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <term>Union types</term>
        ///     <description>
        ///         The oblivious type is the union type where all <see cref="IOneOfPocoType.AllowedTypes"/> are oblivious.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <term>IPoco types</term>
        ///     <description>
        ///         <see cref="ISecondaryPocoType"/>'s oblivious is its non nullable <see cref="IPrimaryPocoType"/>.
        ///         (non nullables <see cref="IAbstractPocoType"/> and <see cref="IPrimaryPocoType"/> are their own oblivious).
        ///     </description>
        ///   </item>
        ///   <item>
        ///     All other types: enum, basic types (non nullable for reference types), any (non nullable), structs (Named record) are their own oblivious.
        ///   </item>
        /// </list>
        /// </summary>
        IPocoType ObliviousType { get; }

        /// <summary>
        /// Gets the nullable associated type (this if <see cref="IsNullable"/> is true).
        /// </summary>
        IPocoType Nullable { get; }

        /// <summary>
        /// Gets the non nullable associated type (this if <see cref="IsNullable"/> is false).
        /// </summary>
        IPocoType NonNullable { get; }

        /// <summary>
        /// Captures a reference from a <see cref="Owner"/> to a <see cref="Type"/>
        /// that is a linked node to another <see cref="NextRef"/> of the same type.
        /// <para>
        /// This captures only the references between types that are not (and cannot be) already exposed
        /// by the types in BOTH ways: this captures how a type is referenced by another fully independent type
        /// thanks to a simple <see cref="Index"/> in the Owner (the referencer) type.
        /// </para>
        /// <para>
        /// This is implementd by an efficient linked list (nodes are often embedded in the owner).
        /// </para>
        /// </summary>
        public interface ITypeRef
        {
            /// <summary>
            /// Next reference to the type.
            /// </summary>
            ITypeRef? NextRef { get; }

            /// <summary>
            /// Gets the owner of this type reference.
            /// Can be a a record or primary Poco <see cref="ICompositePocoType"/> (its fields), a <see cref="ICollectionPocoType"/> (its item types),
            /// a <see cref="IUnionPocoType"/> (its allowed types), a generic <see cref="IAbstractPocoType"/> (its generic arguments) or a
            /// <see cref="IEnumPocoType"/> (its underlying type).
            /// </summary>
            IPocoType Owner { get; }

            /// <summary>
            /// Gets the referenced type.
            /// </summary>
            IPocoType Type { get; }

            /// <summary>
            /// Gets the index of this reference in the <see cref="Owner"/> dedicated list.
            /// <list type="bullet">
            ///     <item>
            ///         For records and primary Poco, this is the index in the <see cref="ICompositePocoType.Fields"/>.
            ///     </item>
            ///     <item>
            ///         For collections, this is the index in the <see cref="ICollectionPocoType.ItemTypes"/>.
            ///     </item>
            ///     <item>
            ///         For <see cref="IAbstractPocoType"/> that have <see cref="IAbstractPocoType.GenericArguments"/> this is
            ///         the index of the generic argument.
            ///     </item>
            ///     <item>
            ///         For <see cref="IUnionPocoType"/> union types this is the index in the <see cref="IOneOfPocoType.AllowedTypes"/>.
            ///     </item>
            ///     <item>
            ///         For <see cref="IEnumPocoType"/> types this is 0 (the <see cref="IEnumPocoType.UnderlyingType"/>.
            ///     </item>
            ///     <item>
            ///         This index is -1 for a back reference from the <see cref="ObliviousType"/>. This applies to (the Owner can be):
            ///         a <see cref="IUnionPocoType"/>, a <see cref="ICollectionPocoType"/> or a <see cref="PocoTypeKind.AnonymousRecord"/>.
            ///     </item>
            /// </list>
            /// This doesn't track the relationship between <see cref="ISecondaryPocoType"/>, <see cref="IAbstractPocoType"/> and
            /// <see cref="IPrimaryPocoType"/>: these relationships are all exposed by the properties of these types
            /// (like <see cref="IAbstractPocoType.Generalizations"/> and <see cref="IPrimaryPocoType.AbstractTypes"/>
            /// <see cref="IAbstractPocoType.AllSpecializations"/>, <see cref="IAbstractPocoType.PrimaryPocoTypes"/>, <see cref="IPrimaryPocoType.SecondaryTypes"/>
            /// and <see cref="ISecondaryPocoType.PrimaryPocoType"/>).
            /// </summary>
            int Index { get; }
        }

        /// <summary>
        /// Gets the head of a linked list of the <see cref="IPocoField"/>, <see cref="ICollectionPocoType.ItemTypes"/>
        /// or <see cref="IOneOfPocoType.AllowedTypes"/> that directly reference this type.
        /// </summary>
        ITypeRef? FirstBackReference { get; }

        /// <summary>
        /// Checks whether this type is the "same type" as <paramref name="type"/>.
        /// <para>
        /// This handles <see cref="ISecondaryPocoType"/> and <see cref="IPrimaryPocoType"/> unification
        /// and only this.
        /// </para>
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is the exact same type as this one or they both belong to the same Poco family.</returns>
        bool IsSamePocoType( IPocoType type );

        /// <summary>
        /// Gets whether the given type is covariant with this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is covariant, false otherwise.</returns>
        bool CanReadFrom( IPocoType type );

        /// <summary>
        /// Gets whether the given type is contravariant with this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is contravariant, false otherwise.</returns>
        bool CanWriteTo( IPocoType type );

        /// <summary>
        /// Returns "[<see cref="Kind"/>]<see cref="CSharpName"/>".
        /// </summary>
        /// <returns>The "[Kind]CSharpName".</returns>
        string ToString();
    }

}

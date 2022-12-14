using CK.Core;
using CommunityToolkit.HighPerformance;
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
        /// in the <see cref="IPocoTypeSystem.AllTypes"/> list.
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
        /// Gets the oblivious type. This type is oblivious in three ways:
        /// <list type="bullet">
        ///   <item>
        ///     <term>Nullable Reference Types</term>
        ///     <description>
        ///         All reference types (including this one if <see cref="Type.IsValueType"/>
        ///         is false) are the <see cref="NonNullable"/> ones.
        ///     </description>
        ///   </item>
        ///   <item>
        ///   <term>Specialized implementation types</term>
        ///   <description>
        ///         Specialized covariant collections are erased to be the regular <see cref="List{T}"/>,
        ///         <see cref="HashSet{T}"/> and <see cref="Dictionary{TKey, TValue}"/>.
        ///   </description>
        ///   </item>
        ///   <item>
        ///   <term>Value tuple field names</term>
        ///   <description>
        ///         Anonymous record field names are erased (<see cref="IRecordPocoField.IsUnnamed"/> is
        ///         always true).
        ///   </description>
        ///   </item>
        /// </list>
        /// <para>
        /// Named composites (<see cref="PocoTypeKind.IPoco"/> and <see cref="PocoTypeKind.Record"/>) are their
        /// own oblivious types (for IPoco, as a reference type, it is the non nullable one).
        /// </para>
        /// <para>
        /// Anonymous records (value tuples) have a "in depth" ObliviousType: field names are erased
        /// and all field types are oblivious.
        /// </para>
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
        /// that is a linked node to another <see cref="NextRef"/> to the same type.
        /// </summary>
        public interface ITypeRef
        {
            /// <summary>
            /// Next reference to the type.
            /// </summary>
            ITypeRef? NextRef { get; }

            /// <summary>
            /// Gets the owner of this type reference.
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
            ///         For records and primary Poco, this is the <see cref="ICompositePocoType.Fields"/>.
            ///         Indexes starts at 0 and are compact: this can be used to handle optimized serialization
            ///         by index (MessagePack) rather than by name (Json).
            ///         <para>
            ///         The generated backing field is named <c>_v{Index}</c> in IPoco generated code.
            ///         </para>
            ///     </item>
            ///     <item>
            ///         For collections, this is the index in the <see cref="ICollectionPocoType.ItemTypes"/>.
            ///     </item>
            ///     <item>
            ///         For union types, this is the index in the <see cref="IOneOfPocoType.AllowedTypes"/>.
            ///     </item>
            /// </list>
            /// </summary>
            int Index { get; }
        }

        /// <summary>
        /// Gets whether this type is exchangeable.
        /// </summary>
        bool IsExchangeable { get; }

        /// <summary>
        /// Gets the head of a linked list of the <see cref="IPocoField"/>, <see cref="ICollectionPocoType.ItemTypes"/>
        /// or <see cref="IOneOfPocoType.AllowedTypes"/> that directly reference this type.
        /// </summary>
        ITypeRef? FirstBackReference { get; }

        /// <summary>
        /// Gets whether the given type is the same as this one: either this <see cref="Type"/> and <see cref="IExtNullabilityInfo.Type"/> are
        /// the same or the generated type for the <paramref name="type"/> would be the same as this one, or the <see cref="IExtNullabilityInfo.Type"/>
        /// is a IPoco interface of the same family as this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="ignoreRootTypeIsNullable">
        /// True to skip this <see cref="IsNullable"/> vs. <paramref name="type"/>'s <see cref="IExtNullabilityInfo.IsNullable"/> check.
        /// </param>
        /// <returns>True if the type is the same, false otherwise.</returns>
        bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false );

        /// <summary>
        /// Gets whether the given type is contravariant with this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is contravariant, false otherwise.</returns>
        bool IsWritableType( IExtNullabilityInfo type );

        /// <summary>
        /// Gets whether the given type is covariant with this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is covariant, false otherwise.</returns>
        bool IsReadableType( IExtNullabilityInfo type );
    }
}

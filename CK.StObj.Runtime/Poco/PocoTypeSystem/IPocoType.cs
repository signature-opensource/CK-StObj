using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets this type's kind.
        /// </summary>
        PocoTypeKind Kind { get; }

        /// <summary>
        /// Gets whether this type has no available implementation in the type system.
        /// <list type="bullet">
        ///     <item>This starts with <see cref="IAbstractPocoType"/> that have no <see cref="IAbstractPocoType.PrimaryPocoTypes"/>.</item>
        ///     <item>Generic <see cref="IAbstractPocoType"/> that have any implementation less <see cref="IAbstractPocoType.GenericArguments"/> type are also implementation less.</item>
        ///     <item><see cref="IUnionPocoType"/> that have all their <see cref="IOneOfPocoType.AllowedTypes"/> implementation less are also implementation less.</item>
        ///     <item>Collections with any implementation less generic parameter types are also implementation less.</item>
        ///     <item>
        ///     Abstract read only collections (<see cref="ICollectionPocoType.IsAbstractReadOnly"/>) are implementation less.
        ///     As these beasts resides on the C# side (as abstract readonly properties), it is rather useless to expose them:
        ///     by considering them implementation less, they are excluded from any IPocoTypeSet.
        ///     </item>
        /// </list>
        /// <para>
        /// Implementation less types exist on the C# side and are modelized but are unused extension points.
        /// </para>
        /// </summary>
        bool ImplementationLess { get; }

        /// <summary>
        /// Gets whether this type is polymorphic.
        /// Polymorphic types are <see cref="PocoTypeKind.Any"/>, <see cref="PocoTypeKind.AbstractPoco"/>
        /// and <see cref="PocoTypeKind.UnionType"/> (and the <see cref="PocoTypeKind.Basic"/> <see cref="ExtendedCultureInfo"/>
        /// because it can be a <see cref="NormalizedCultureInfo"/>).
        /// <para>
        /// <see cref="ICollectionPocoType.IsAbstractReadOnly"/> are also polymorphic (but <see cref="ICollectionPocoType.IsAbstractCollection"/>
        /// are not).
        /// </para>
        /// </summary>
        [MemberNotNullWhen(false,nameof(StructuralFinalType))]
        bool IsPolymorphic { get; }

        /// <summary>
        /// Gets whether this type can be used in a <see cref="ISet{T}"/> or as a key in a <see cref="IDictionary{TKey, TValue}"/>.
        /// Note that to be a key in a dictionary, the key type must also be non nullable and non polymorphic (<see cref="IsNullable"/>
        /// and <see cref="IsPolymorphic"/> must be false).
        /// <para>
        /// A reference type must be immutable and have a value equality semantics, either by implementing <see cref="IEquatable{T}"/>
        /// (like the <see cref="string"/>) or because the instance is unique by design, like the <see cref="ExtendedCultureInfo"/>
        /// and <see cref="NormalizedCultureInfo"/>.
        /// </para>
        /// <para>
        /// Value types can be immutable (all <see cref="PocoTypeKind.Basic"/> value types are immutable) but anonymous or named records that
        /// have no mutable reference types are also readonly compliant since a copy of the value is de facto a "read-only" projection of its source
        /// in the sense that it cannot be used to mutate the source data.
        /// </para>
        /// <para>
        /// Mutable types like Poco and collections are not read-only compliant and cannot be used in a set or as a dictionary key. 
        /// </para>
        /// </summary>
        bool IsReadOnlyCompliant { get; }

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
        /// Gets the C# name with namespaces and nullabilities of this type.
        /// </summary>
        string CSharpName { get; }

        /// <summary>
        /// Gets the implementation C# type name for this type.
        /// </summary>
        string ImplTypeName { get; }

        /// <summary>
        /// Gets whether this type is oblivious. See <see cref="ObliviousType"/>.
        /// </summary>
        bool IsOblivious { get; }

        /// <summary>
        /// Gets the oblivious type (this instance if <see cref="IsOblivious"/> is true).
        /// Oblivious types are actual C# types.
        /// <list type="bullet">
        ///   <item>
        ///     <term>Nullable Reference Types</term>
        ///     <description>
        ///         Oblivious type of a reference type is always the <see cref="Nullable"/>.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <term>Collection types</term>
        ///     <description>
        ///         Abstract collections (readonly or not) are mapped to their equivalent type where generic
        ///         parameters are oblivious except for the dictionary key that is always non nullable (ie. for
        ///         a reference type this is not the oblivious type - note that only some <see cref="IBasicRefPocoType"/>
        ///         can be dictionary key).
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
        ///         The oblivious type is the union type where all <see cref="IOneOfPocoType.AllowedTypes"/> are oblivious
        ///         and is the nullable (a union type is considered as a reference type).
        ///     </description>
        ///   </item>
        ///   <item>
        ///     All other types: enum, basic types (nullable for reference types), any (nullable), structs (Named record),
        ///     <see cref="IAbstractPocoType"/>, <see cref="ISecondaryPocoType"/> and <see cref="IPrimaryPocoType"/> are their own oblivious.
        ///   </item>
        /// </list>
        /// </summary>
        IPocoType ObliviousType { get; }

        /// <summary>
        /// Gets whether this type is regular. See <see cref="RegularType"/>.
        /// </summary>
        [MemberNotNullWhen( true, nameof( RegularType ) )]
        bool IsRegular { get; }

        /// <summary>
        /// Gets the regular type (this instance if <see cref="IsRegular"/> is true) with the same nullablility as this one.
        /// Types are generally their own regular except for:
        /// <list type="bullet">
        ///     <item>
        ///     <term>Anonymous records (value tuples)</term>
        ///     <description>Their regular has no field names and their field types are regular.</description>
        ///     </item>
        ///     <item>
        ///     <term>Collection</term>
        ///     <description>Their regular are concrete collections (array, List, HashSet and Dictionary) and their generic arguments are regular.</description>
        ///     </item>
        ///     <item>
        ///     <term>Abstract read-only Collection</term>
        ///     <description>Their regular is null (like their <see cref="StructuralFinalType"/>).</description>
        ///     </item>
        /// </list>
        /// </summary>
        IPocoType? RegularType { get; }

        /// <summary>
        /// Gets whether this is a final type. See <see cref="StructuralFinalType"/>.
        /// Only non nullable value type and nullable reference type can be final.
        /// </summary>
        [MemberNotNullWhen( true, nameof( StructuralFinalType ) )]
        bool IsStructuralFinalType { get; }

        /// <summary>
        /// Gets the final type associated to this type (this instance if <see cref="IsStructuralFinalType"/> is true)
        /// even if <see cref="ImplementationLess"/> is true.
        /// Usually <see cref="FinalType"/>, that considers only false <see cref="ImplementationLess"/>, should be used.
        /// <para>
        /// This is never null when <see cref="IsPolymorphic"/> is false.
        /// </para>
        /// The set of the Final types is a subset of the Oblivious types.
        /// <list type="bullet">
        ///     <item>Final type of a value type is its non nullable.</item>
        ///     <item>Final type of a reference type is either null or its nullable (oblivious reference types are nullable).</item>
        ///     <item><see cref="PocoTypeKind.Any"/>, <see cref="PocoTypeKind.AbstractPoco"/> and <see cref="PocoTypeKind.UnionType"/> have no final type.</item>
        ///     <item>Abstract read only collections (see <see cref="ICollectionPocoType.IsAbstractReadOnly"/>) have no final type.</item>
        ///     <item>A <see cref="IBasicRefPocoType"/> with an abstract <see cref="Type"/> has no final type.</item>
        ///     <item>
        ///     For mutable collections, it is the nullable equivalent type with oblivious generic arguments.
        ///     </item>
        ///     <item>
        ///     For <see cref="ISecondaryPocoType"/> it is its <see cref="IPrimaryPocoType"/> (that is oblivious, hence nullable).
        ///     </item>
        ///     <item>For <see cref="PocoTypeKind.Enum"/>, <see cref="PocoTypeKind.Record"/> it is their non nullable.</item>
        ///     <item>For <see cref="PocoTypeKind.AnonymousRecord"/> it is the oblivious's non nullable.</item>
        /// </list>
        /// <para>
        /// A final type can be based on types that are not final: <c>List&lt;object&gt;</c> or <c>List&lt;int?&gt;</c> are final
        /// types even if <c>object</c> and <c>int?</c> are not.
        /// </para>
        /// <para>
        /// As Oblivious types, Final types correspond to C# types but are more restrictive as they capture the types that can
        /// be discovered on a non null object instance: they are the subset of types that can be mapped to the result of a
        /// call to <see cref="object.GetType()"/>.
        /// </para>
        /// </summary>
        IPocoType? StructuralFinalType { get; }

        /// <summary>
        /// Gets whether this type is it its own <see cref="FinalType"/>.
        /// Only non nullable value type and nullable reference type can be final.
        /// </summary>
        [MemberNotNullWhen( true, nameof( FinalType ) )]
        bool IsFinalType { get; }

        /// <summary>
        /// Gets the final type associated to this. Null if <see cref="ImplementationLess"/> is true otherwise
        /// it is the <see cref="StructuralFinalType"/> (that can be null).
        /// </summary>
        IPocoType? FinalType { get; }

        /// <summary>
        /// Gets whether this type exists in its serializable form: it is non nullable, regular and is either final or its nullable is final.
        /// <see cref="PocoTypeKind.Any"/>, <see cref="PocoTypeKind.AbstractPoco"/> and <see cref="PocoTypeKind.UnionType"/> are never observable (thay have no final type).
        /// <para>
        /// There is no <c>SerializedObservableType</c> and this is intended as it would introduce an ambiguity
        /// regarding the final type that will be selected for abstract collection (<see cref="ICollectionPocoType.IsAbstractCollection"/>):
        /// the final type of an abstract collection is itself when the collection is implemented by an adapter, but this final type is
        /// not observable in the serialization, it is it's <see cref="RegularType"/> associated collection that is observable.
        /// </para>
        /// <para>
        /// To obtain the "serialized observable" type, one can always use <c>Regular?.FinalType?.NonNullable</c>. 
        /// This makes the regular type mapping explicit.
        /// </para>
        /// </summary>
        bool IsSerializedObservable { get; }

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
        /// or <see cref="IOneOfPocoType.AllowedTypes"/> that directly references this type.
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
        /// Gets whether the <paramref name="type"/> is a super type of this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is covariant, false otherwise.</returns>
        bool IsSubTypeOf( IPocoType type );

        /// <summary>
        /// Returns "[<see cref="Kind"/>]<see cref="CSharpName"/>".
        /// </summary>
        /// <returns>The "[Kind]CSharpName".</returns>
        string ToString();
    }

}

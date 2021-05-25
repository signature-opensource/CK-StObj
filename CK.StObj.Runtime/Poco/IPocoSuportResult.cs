using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Exposes the result of <see cref="IPoco"/> interfaces support.
    /// This is available in the <see cref="IGeneratedBinPath.ServiceContainer"/> of the
    /// <see cref="ICodeGenerationContext.CurrentRun"/>.
    /// </summary>
    public interface IPocoSupportResult
    {
        /// <summary>
        /// Gets the root Poco information.
        /// </summary>
        IReadOnlyList<IPocoRootInfo> Roots { get; }

        /// <summary>
        /// Gets the root poco information indexed by their <see cref="IPocoRootInfo.Name"/>
        /// and <see cref="IPocoRootInfo.PreviousNames"/>.
        /// </summary>
        IReadOnlyDictionary<string, IPocoRootInfo> NamedRoots { get; }

        /// <summary>
        /// Gets the <see cref="IPocoInterfaceInfo"/> for any <see cref="IPoco"/> interface.
        /// </summary>
        /// <param name="pocoInterface">The IPoco interface.</param>
        /// <returns>Information about the interface. Null if not found.</returns>
        IPocoInterfaceInfo? Find( Type pocoInterface );

        /// <summary>
        /// Gets the dictionary of all Poco interfaces indexed by their <see cref="IPocoInterfaceInfo.PocoInterface"/>.
        /// </summary>
        IReadOnlyDictionary<Type, IPocoInterfaceInfo> AllInterfaces { get; }

        /// <summary>
        /// Gets the dictionary of all interface types that are not <see cref="IPoco"/> but are supported by at least one Poco, mapped
        /// to the list of roots that support them.
        /// <para>
        /// Keys are not <see cref="IPoco"/> interfaces (technically they may be IPoco but then they are "canceled" by
        /// a <see cref="CKTypeDefinerAttribute"/> or <see cref="CKTypeSuperDefinerAttribute"/>): this set complements and
        /// doesn't intersect <see cref="AllInterfaces"/>.
        /// </para>
        /// <para>
        /// Note that <see cref="IPoco"/> and <see cref="IClosedPoco"/> are excluded from this set (as well as from the AllInterfaces).
        /// </para>
        /// </summary>
        IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces { get; }

        /// <summary>
        /// Handles the <paramref name="from"/>'s <see cref="IPocoPropertyInfo.PropertyUnionTypes"/> (if any): each
        /// of them must be assignable (and nullability compatible) to the <paramref name="target"/> Poco property.
        /// </summary>
        /// <param name="target">The target Poco property.</param>
        /// <param name="from">The source Poco property.</param>
        /// <returns>True if target is assignable from source.</returns>
        bool IsAssignableFrom( IPocoPropertyInfo target, IPocoPropertyInfo from );

        /// <summary>
        /// Handles the target's <see cref="IPocoPropertyInfo.PropertyUnionTypes"/> (if any): at least
        /// one of them must be assignable (and nullability compatible) with the <paramref name="from"/>
        /// type for the Poco property to be assignable.
        /// </summary>
        /// <param name="target">The target Poco property.</param>
        /// <param name="from">The source type.</param>
        /// <param name="fromNullability">The source nullability.</param>
        /// <returns>True if target is assignable from source.</returns>
        bool IsAssignableFrom( IPocoPropertyInfo target, Type from, NullabilityTypeKind fromNullability );

        /// <summary>
        /// Extends the standard <see cref="Type.IsAssignableFrom(Type?)"/> by checking if
        /// both <paramref name="target"/> and <paramref name="from"/> belong to the same
        /// Poco's <see cref="IPocoRootInfo.Interfaces"/> (and that a non nullable target cannot
        /// be assigned from a nullable source).
        /// </summary>
        /// <param name="target">The target type</param>
        /// <param name="targetNullability">The target nullability: A non nullable cannot be assigned from a nullable.</param>
        /// <param name="from">The source type.</param>
        /// <param name="fromNullability">The source nullability.</param>
        /// <returns>True if target is assignable from source.</returns>
        bool IsAssignableFrom( Type target, NullabilityTypeKind targetNullability, Type from, NullabilityTypeKind fromNullability );

    }
}

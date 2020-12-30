using CK.Core;
using System;
using System.Collections.Generic;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Exposes the result of <see cref="IPoco"/> interfaces support.
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
        /// Keys are not <see cref="IPoco"/> interfaces (technically they may be IPoco but then they are "cancelled" by
        /// a <see cref="CKTypeDefinerAttribute"/> or <see cref="CKTypeSuperDefinerAttribute"/>): this set complements and
        /// doesn't intersect <see cref="AllInterfaces"/>.
        /// </para>
        /// <para>
        /// Note that <see cref="IPoco"/> and <see cref="IClosedPoco"/> are excluded from this set (as well as from the AllInterfaces).
        /// </para>
        /// </summary>
        IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces { get; }

    }
}

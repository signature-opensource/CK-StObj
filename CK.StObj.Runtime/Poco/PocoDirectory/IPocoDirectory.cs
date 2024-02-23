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
    public interface IPocoDirectory
    {
        /// <summary>
        /// Gets the root Poco information.
        /// </summary>
        IReadOnlyList<IPocoFamilyInfo> Families { get; }

        /// <summary>
        /// Gets the root poco information indexed by their <see cref="IPocoFamilyInfo.Name"/>
        /// and <see cref="IPocoFamilyInfo.PreviousNames"/>.
        /// </summary>
        IReadOnlyDictionary<string, IPocoFamilyInfo> NamedFamilies { get; }

        /// <summary>
        /// Gets the <see cref="IPocoInterfaceInfo"/> for any "concrete" <see cref="IPoco"/> interface.
        /// </summary>
        /// <param name="pocoInterface">The IPoco interface.</param>
        /// <returns>Information about the interface. Null if not found: the interface may be an "abstract" one.</returns>
        IPocoInterfaceInfo? Find( Type pocoInterface );

        /// <summary>
        /// Gets the dictionary of all IPoco "concrete" interfaces indexed by their <see cref="IPocoInterfaceInfo.PocoInterface"/>.
        /// </summary>
        IReadOnlyDictionary<Type, IPocoInterfaceInfo> AllInterfaces { get; }

        /// <summary>
        /// Gets the dictionary of all "abstract" interface types that are supported by
        /// at least one Poco family, mapped to the list of families that support them.
        /// <para>
        /// Keys are not <see cref="IPoco"/> interfaces (technically they are IPoco but they are "canceled" by
        /// a <see cref="CKTypeDefinerAttribute"/> or <see cref="CKTypeSuperDefinerAttribute"/>): this set complements and
        /// doesn't intersect <see cref="AllInterfaces"/>.
        /// </para>
        /// <para>
        /// Note that <see cref="IPoco"/> is excluded from this set (as well as from the AllInterfaces).
        /// </para>
        /// </summary>
        IReadOnlyDictionary<Type, IReadOnlyList<IPocoFamilyInfo>> OtherInterfaces { get; }
    }
}

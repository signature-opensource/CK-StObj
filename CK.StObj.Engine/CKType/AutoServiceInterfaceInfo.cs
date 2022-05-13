using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Service type descriptor exists only if the type is not excluded (excluding a
    /// service type is like removing the <see cref="IAutoService"/> interface marker from
    /// its interfaces) and has at least one implementation that <see cref="AutoServiceClassInfo.IsIncluded"/>.
    /// </summary>
    public class AutoServiceInterfaceInfo
    {
        /// <summary>
        /// The interface type.
        /// </summary>
        public Type Type => Attributes.Type;

        /// <summary>
        /// Gets the attribute cache for this type.
        /// </summary>
        public TypeAttributesCache Attributes { get; }

        /// <summary>
        /// Gets the initial type kind that is the result of the marker interfaces, attributes
        /// on the type itself and of any setting done through <see cref="CKTypeKindDetector.SetAutoServiceKind(IActivityMonitor, Type, AutoServiceKind)"/>
        /// before types registration.
        /// This is always valid since otherwise the AutoServiceInterfaceInfo is not instantiated.
        /// </summary>
        public CKTypeKind InitialTypeKind { get; }

        /// <summary>
        /// The interface type.
        /// </summary>
        public readonly int SpecializationDepth;

        /// <summary>
        /// Gets whether this service interface is specialized at least by
        /// one other interface.
        /// </summary>
        public bool IsSpecialized { get; private set; }

        /// <summary>
        /// Gets the final resolved class that implements this interface.
        /// This is set at the end of the process during interface resolution and
        /// is used to handle actual lifetime checks without requiring another
        /// dictionary index.
        /// </summary>
        public AutoServiceClassInfo? FinalResolved { get; internal set; }

        /// <summary>
        /// Gets the base service interfaces that are specialized by this one.
        /// Never null and often empty.
        /// </summary>
        public readonly IReadOnlyList<AutoServiceInterfaceInfo> Interfaces;

        /// <summary>
        /// Overridden to return a readable string.
        /// </summary>
        /// <returns>Readable string.</returns>
        public override string ToString() => $"{(IsSpecialized ? "[Specialized]" : "")}{Type}";


        internal AutoServiceInterfaceInfo( TypeAttributesCache type, CKTypeKind lt, IEnumerable<AutoServiceInterfaceInfo> baseInterfaces )
        {
            Debug.Assert( lt == CKTypeKind.IsAutoService
                            || lt == (CKTypeKind.IsAutoService | CKTypeKind.IsSingleton)
                            || lt == (CKTypeKind.IsAutoService | CKTypeKind.IsScoped) );
            Attributes = type;
            InitialTypeKind = lt;
            AutoServiceInterfaceInfo[] bases = Array.Empty<AutoServiceInterfaceInfo>();
            int depth = 0;
            foreach( var iT in baseInterfaces )
            {
                depth = Math.Max( depth, iT.SpecializationDepth + 1 );
                Array.Resize( ref bases, bases.Length + 1 );
                bases[bases.Length - 1] = iT;
                iT.IsSpecialized = true;
            }
            SpecializationDepth = depth;
            Interfaces = bases;
        }

    }
}

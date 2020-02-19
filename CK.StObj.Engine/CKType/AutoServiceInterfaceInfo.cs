using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        public Type Type { get; }

        /// <summary>
        /// Gets this Service interface life time.
        /// This reflects the <see cref="IAutoService"/> or <see cref="ISingletonAutoService"/>
        /// vs. <see cref="IScopedAutoService"/> interface marker.
        /// This can never be <see cref="CKTypeKindExtension.IsNoneOrInvalid(CKTypeKind)"/> since
        /// in such cases, the AutoServiceInterfaceInfo is not instanciated.
        /// </summary>
        public CKTypeKind DeclaredLifetime { get; }

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
        public AutoServiceClassInfo FinalResolved { get; internal set; }

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


        internal AutoServiceInterfaceInfo( Type t, CKTypeKind lt, IEnumerable<AutoServiceInterfaceInfo> baseInterfaces )
        {
            Debug.Assert( lt == CKTypeKind.IsAutoService || lt == CKTypeKind.AutoSingleton || lt == CKTypeKind.AutoScoped );
            Type = t;
            DeclaredLifetime = lt;
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

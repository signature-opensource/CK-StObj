using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Internal interface supported by <see cref="CKTypeCollector"/> that limits its exposure
    /// to the final <see cref="AutoServiceClassInfo.ComputeFinalTypeKind"/> method.
    /// <para>
    /// This is only to avoid the computation to have access to the whole <see cref="CKTypeCollector"/>.
    /// </para>
    /// </summary>
    interface IAutoServiceKindComputeFacade
    {
        /// <summary>
        /// Exposes the <see cref="CKTypeKindDetector"/>.
        /// </summary>
        CKTypeKindDetector KindDetector { get; }

        /// <summary>
        /// Gets the <see cref="CKTypeCollector.MultipleImpl"/>.
        /// Used by AutoService final registration.
        /// </summary>
        /// <param name="enumeratedType">The type of enumerated multiple interface.</param>
        /// <returns>The descriptor if it exists.</returns>
        CKTypeCollector.MultipleImpl? GetMultipleInterfaceDescriptor( Type enumeratedType );

        /// <summary>
        /// This has absolutely nothing to do here :(.
        /// This is used to set this on the engine StObjMap... This is awful.
        /// </summary>
        IReadOnlyDictionary<Type, IStObjMultipleInterface> MultipleMappings { get; }

        /// <summary>
        /// Once all AutoServiceClassInfo have successfully called
        /// <see cref="CKTypeCollector.MultipleImpl.ComputeFinalTypeKind(Core.IActivityMonitor, IAutoServiceKindComputeFacade, ref bool)"/>
        /// this is called to ensure that all IEnumerable of IsMultiple interfaces lifetime is computed.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <returns>True on success, false on error.</returns>
        bool EnsureMultipleComputedKind( IActivityMonitor monitor );
    }
}

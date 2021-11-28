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
        /// </summary>
        /// <param name="enumeratedType">The type of enumerated multiple interface.</param>
        /// <returns>The descriptor if it exists.</returns>
        CKTypeCollector.MultipleImpl? GetMultipleInterfaceDescriptor( Type enumeratedType );

    }
}

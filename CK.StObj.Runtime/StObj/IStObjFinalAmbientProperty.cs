#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjFinalAmbientProperty.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Setup
{
    /// <summary>
    /// Exposes an Ambient property that has not been resolved. It can be set by <see cref="IStObjValueResolver.ResolveExternalPropertyValue"/>.
    /// </summary>
    public interface IStObjFinalAmbientProperty : IStObjAmbientProperty
    {
        /// <summary>
        /// Gets the current value (<see cref="System.Type.Missing"/> as long as <see cref="SetValue"/> has not been called).
        /// </summary>
        object Value { get; }

        /// <summary>
        /// Sets a value for this property.
        /// </summary>
        /// <param name="value">Value to set. Type must be compatible.</param>
        void SetValue( object value );

    }
}

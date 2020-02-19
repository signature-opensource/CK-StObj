#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjValueResolver.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Enables explicit configuration of StObjConstruct method parameters as well as manual resolution for ambient 
    /// properties that are not bound to <see cref="IStObjResult"/> objects. 
    /// Must be passed as a parameter to the constructor of StObjCollector.
    /// </summary>
    public interface IStObjValueResolver
    {
        /// <summary>
        /// Dynamically called before ordering of the whole graph for each ambient property only if automatic resolution failed to locate a StObj.
        /// The <see cref="IStObjFinalAmbientProperty.SetValue"/> can be used to set the property value.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ambientProperty">Property for which a value should be set.</param>
        void ResolveExternalPropertyValue( IActivityMonitor monitor, IStObjFinalAmbientProperty ambientProperty );

        /// <summary>
        /// Dynamically called for each parameter right before invoking the construct method.
        /// The <see cref="IStObjFinalParameter.SetParameterValue"/> can be used to set or alter the parameter value.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="parameter">Parameter of a StObjConstruct method.</param>
        void ResolveParameterValue( IActivityMonitor monitor, IStObjFinalParameter parameter );
        
    }

}

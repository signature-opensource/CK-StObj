#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjStructuralConfigurator.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// This interface allows dynamic configuration of items.
    /// It can be supported by attributes (to be aplied on Structured Object type or on its members) or be 
    /// used globally as a configuration of StObjCollector object.
    /// </summary>
    public interface IStObjStructuralConfigurator
    {
        /// <summary>
        /// Enables configuration of items before setup process.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The item to configure.</param>
        void Configure( IActivityMonitor monitor, IStObjMutableItem o );
    }
}

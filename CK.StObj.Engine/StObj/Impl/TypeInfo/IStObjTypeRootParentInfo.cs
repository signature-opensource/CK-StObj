#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TypeInfo\IStObjTypeInfoFromParent.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// This interface collects information specific to base types (above real objects root).
    /// </summary>
    internal interface IStObjTypeRootParentInfo
    {
        /// <summary>
        /// Gets the StObjConstruct methods (and a capture of their parameters) from top
        /// down to the root of the real objects serialization path.
        /// </summary>
        IReadOnlyList<(MethodInfo, ParameterInfo[])> StObjConstructCollector { get; }

        /// <summary>
        /// Gets the StObjInitialize methods from top down to the root of the real
        /// objects serialization path.
        /// </summary>
        IReadOnlyList<MethodInfo> StObjInitializeCollector { get; }

        /// <summary>
        /// Gets the RegisterStartupServices methods from top down to the root of the real objects serialization path.
        /// </summary>
        IReadOnlyList<MethodInfo> RegisterStartupServicesCollector { get; }


        /// <summary>
        /// Gets the ConfigureServices methods (and a capture of their parameters) from top
        /// down to the root of the real objects serialization path.
        /// </summary>
        IReadOnlyList<ParameterInfo[]> ConfigureServicesCollector { get; }
    }
}

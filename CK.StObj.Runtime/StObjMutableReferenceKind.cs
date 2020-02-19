#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\StObjMutableReferenceKind.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Setup
{
    /// <summary>
    /// Describes the different kind of <see cref="IStObjReference"/>.
    /// </summary>
    [Flags]
    public enum StObjMutableReferenceKind
    {
        /// <summary>
        /// Non applicable.
        /// </summary>
        None = 0,

        /// <summary>
        /// Container reference.
        /// </summary>
        Container = 1,
        
        /// <summary>
        /// Requires reference.
        /// </summary>
        Requires = 2,

        /// <summary>
        /// RequiredBy reference.
        /// </summary>
        RequiredBy = 4,

        /// <summary>
        /// Group reference.
        /// </summary>
        Group = 8,

        /// <summary>
        /// Child reference.
        /// </summary>
        Child = 16,

        /// <summary>
        /// Parameter from StObjConstruct method. It is a considered as a Requires.
        /// </summary>
        ConstructParameter = 32,

        /// <summary>
        /// Ambient property.
        /// This kind of reference can depend on the referenced StObj (see <see cref="Core.TrackAmbientPropertiesMode"/>).
        /// </summary>
        AmbientProperty = 64,

        /// <summary>
        /// Pure reference to another object without any structural constraint.
        /// </summary>
        RealObject = 128,

    }
}

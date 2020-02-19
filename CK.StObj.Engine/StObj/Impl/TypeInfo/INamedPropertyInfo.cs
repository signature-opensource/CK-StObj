#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TypeInfo\INamedPropertyInfo.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Setup
{
    /// <summary>
    /// Unifies <see cref="CovariantPropertyInfo"/> and <see cref="StObjPropertyInfo"/>.
    /// </summary>
    internal interface INamedPropertyInfo
    {
        string Name { get; }

        Type DeclaringType { get; }

        string Kind { get; }
    }
}

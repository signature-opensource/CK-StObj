#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TypeInfo\IStObjTypeInfoFromParent.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// This interface applies to base types (above real objects root) as well as <see cref="RealObjectClassInfo"/>
    /// (via the <see cref="RealObjectClassInfo.Generalization"/>).
    /// </summary>
    internal interface IStObjTypeInfoFromParent
    {
        int SpecializationDepth { get; }
        Type Container { get; }
        IReadOnlyList<AmbientPropertyInfo> AmbientProperties { get; }
        IReadOnlyList<InjectObjectInfo> InjectObjects { get; }
        IReadOnlyList<StObjPropertyInfo> StObjProperties { get; }
        DependentItemKind ItemKind { get; }
        TrackAmbientPropertiesMode TrackAmbientProperties { get; }
    }
}

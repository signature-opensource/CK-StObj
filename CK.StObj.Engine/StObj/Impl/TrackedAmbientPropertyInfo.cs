#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TrackedAmbientPropertyInfo.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;

namespace CK.Setup
{
    class TrackedAmbientPropertyInfo : IStObjTrackedAmbientPropertyInfo
    {
        public readonly MutableItem Owner;
        public readonly AmbientPropertyInfo AmbientPropertyInfo;

        internal TrackedAmbientPropertyInfo( MutableItem o, AmbientPropertyInfo p )
        {
            Owner = o;
            AmbientPropertyInfo = p;
        }

        IStObjResult IStObjTrackedAmbientPropertyInfo.Owner
        {
            get { return Owner; }
        }

        PropertyInfo IStObjTrackedAmbientPropertyInfo.PropertyInfo
        {
            get { return AmbientPropertyInfo.PropertyInfo; }
        }
    }
}

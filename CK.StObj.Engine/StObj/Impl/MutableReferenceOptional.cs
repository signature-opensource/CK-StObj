#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\MutableReferenceOptional.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using CK.Core;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Base class for construct parameters, ambient properties or Ambient inject.
    /// </summary>
    internal abstract class MutableReferenceOptional : MutableReference
    {
        MutableItem _resolved;

        internal MutableReferenceOptional( MutableItem owner, StObjMutableReferenceKind kind )
            : base( owner, kind )
        {
            _resolved = UnresolvedMarker;
        }

        public abstract string Name { get; }

        internal abstract string KindName { get; }

        internal abstract Type UnderlyingType { get; }

        public bool IsOptional { get; set; }

        internal MutableItem CachedResolvedStObj 
        { 
            get 
            {
                Debug.Assert( _resolved != UnresolvedMarker, "ResolveToStObj must have been called before." );
                return _resolved; 
            } 
        }

        internal override MutableItem ResolveToStObj( IActivityMonitor monitor, StObjObjectEngineMap collector )
        {
            if( _resolved != UnresolvedMarker ) return _resolved;
            if( Type == null && !IsOptional )
            {
                Error( monitor, $"Type can not be null since the {KindName} is not optional" );
                return _resolved = null;
            }
            Debug.Assert( Type != null || IsOptional );
            if( Type != null && !UnderlyingType.IsAssignableFrom( Type ) )
            {
                Error( monitor, $"Type '{Type.FullName}' is not compatible with the {KindName} type ('{UnderlyingType.FullName}')" );
                return _resolved = null;
            }
            return _resolved = base.ResolveToStObj( monitor, collector );
        }

        protected override void WarnOrErrorIfStObjRequired(IActivityMonitor monitor, bool skipWarnOnValueType, string text)
        {
            if( !IsOptional ) base.WarnOrErrorIfStObjRequired(monitor, skipWarnOnValueType, text);
        }
    }
}

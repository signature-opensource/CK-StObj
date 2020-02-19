#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\MutableItem.StructuredObject.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CK.Core;

namespace CK.Setup
{

    partial class MutableItem
    {

        public object CreateStructuredObject( IActivityMonitor monitor, IStObjRuntimeBuilder runtimeBuilder )
        {
            Debug.Assert( Specialization == null );
            Debug.Assert( _leafData.StructuredObject == null, "Called once and only once." );
            try
            {
                return _leafData.CreateStructuredObject( runtimeBuilder, ObjectType );
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
                return null;
            }
        }

        /// <summary>
        /// Gets the properties to set right before the call to StObjConstruct.
        /// Properties are registered at the root object, the Property.DeclaringType can be used to
        /// target the correct type in the inheritance chain.
        /// </summary>
        public IReadOnlyList<PropertySetter> PreConstructProperties => _preConstruct;

        /// <summary>
        /// Gets the post build properties to set. Potentially not null only on leaves.
        /// </summary>
        public IReadOnlyList<PropertySetter> PostBuildProperties => _leafData?.PostBuildProperties;

        internal void RegisterRemainingDirectPropertiesAsPostBuildProperties( BuildValueCollector valueCollector )
        {
            if( Specialization == null && _leafData.DirectPropertiesToSet != null )
            {
                foreach( var k in _leafData.DirectPropertiesToSet )
                {
                    if( k.Value != System.Type.Missing ) AddPostBuildProperty( k.Key, k.Value, valueCollector );
                }
                _leafData.DirectPropertiesToSet.Clear();
            }
        }
    }
}

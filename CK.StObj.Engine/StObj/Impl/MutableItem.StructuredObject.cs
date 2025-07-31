using System;
using System.Collections.Generic;
using System.Diagnostics;
using CK.Core;

namespace CK.Setup;


partial class MutableItem
{

    public object? CreateStructuredObject( IActivityMonitor monitor )
    {
        Throw.DebugAssert( Specialization == null );
        Throw.DebugAssert( "Called once and only once.", _leafData.StructuredObject == null );
        try
        {
            return _leafData.CreateStructuredObject( ClassType );
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
    public IReadOnlyList<PropertySetter>? PostBuildProperties => _leafData?.PostBuildProperties;

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

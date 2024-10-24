using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Engine.TypeCollector;


sealed class ConfiguredTypeSet : IConfiguredTypeSet
{
    readonly HashSet<ICachedType> _allTypes;
    readonly ExternalTypeConfigurationSet _configuredTypes;

    public ConfiguredTypeSet()
    {
        _allTypes = new HashSet<ICachedType>();
        _configuredTypes = new ExternalTypeConfigurationSet();
    }

    // Copy constructor.
    public ConfiguredTypeSet( IConfiguredTypeSet configuredTypes )
    {
        _allTypes = new HashSet<ICachedType>( configuredTypes.AllTypes );
        _configuredTypes = new ExternalTypeConfigurationSet( configuredTypes.ConfiguredTypes );
    }

    public IReadOnlySet<ICachedType> AllTypes => _allTypes;

    public IReadOnlyCollection<ExternalTypeConfiguration> ConfiguredTypes => _configuredTypes;

    public void Add( ISet<ICachedType> types ) => _allTypes.UnionWith( types );

    public void AddRange( IEnumerable<ICachedType> types ) => _allTypes.AddRange( types );

    // Beware! Only one | here, we want to remove from both of them.
    public bool Remove( ICachedType type ) => _allTypes.Remove( type ) | _configuredTypes.Remove( type.Type );

    public bool Add( IActivityMonitor monitor, string sourceName, ICachedType type, ExternalServiceKind kind )
    {
        if( kind == ExternalServiceKind.None )
        {
            return _allTypes.Add( type );
        }
        if( _configuredTypes.AsDictionary.TryGetValue( type.Type, out var exists ) )
        {
            if( exists == kind ) return false;
            monitor.Info( $"{sourceName} updated '{type:N}' from '{exists}' to {kind}." );
        }
        _allTypes.Add( type );
        _configuredTypes.Add( type.Type, kind );
        CheckInvariants( this );
        return true;
    }

    public void Add( IActivityMonitor monitor, ConfiguredTypeSet other, string sourceName )
    {
        CheckInvariants( other );
        _allTypes.UnionWith( other.AllTypes );
        foreach( var o in other._configuredTypes.AsDictionary )
        {
            if( _configuredTypes.AsDictionary.TryGetValue( o.Key, out var exists ) && exists != o.Value )
            {
                monitor.Info( $"{sourceName} updated '{o.Key:N}' from '{exists}' to {o.Value}." );
            }
            _configuredTypes.Add( o.Key, o.Value );
        }
        CheckInvariants( this );
    }

    [Conditional( "DEBUG" )]
    static void CheckInvariants( ConfiguredTypeSet set )
    {
        // Cannot use the IsSupersetOf of set.AllTypes (of ICacheType) because we don't have the GlobalTypeCache here.
        // Hopefully, this is in Debug only.
        var types = new HashSet<Type>( set._allTypes.Select( cT => cT.Type ) );
        Throw.DebugAssert( types.IsSupersetOf( set._configuredTypes.AsDictionary.Keys ) );
        Throw.DebugAssert( !set._configuredTypes.AsDictionary.Values.Any( k => k == ExternalServiceKind.None ) );
    }
}

using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CK.Engine.TypeCollector;


sealed class ConfiguredTypeSet : IConfiguredTypeSet
{
    readonly HashSet<Type> _allTypes;
    readonly TypeConfigurationSet _configuredTypes;

    public ConfiguredTypeSet()
    {
        _allTypes = new HashSet<Type>();
        _configuredTypes = new TypeConfigurationSet();
    }

    // Copy constructor.
    public ConfiguredTypeSet( IConfiguredTypeSet configuredTypes )
    {
        _allTypes = new HashSet<Type>( configuredTypes.AllTypes );
        _configuredTypes = new TypeConfigurationSet( configuredTypes.ConfiguredTypes );
    }

    internal ConfiguredTypeSet( HashSet<Type> allTypes )
    {
        _allTypes = allTypes;
        _configuredTypes = new TypeConfigurationSet();
    }

    public IReadOnlySet<Type> AllTypes => _allTypes;

    public IReadOnlyCollection<TypeConfiguration> ConfiguredTypes => _configuredTypes;

    public void Add( ISet<Type> types ) => _allTypes.UnionWith( types );

    public void AddRange( IEnumerable<Type> types ) => _allTypes.AddRange( types );

    // Beware! Only one | here, we want to remove from both of them.
    public bool Remove( Type type ) => _allTypes.Remove( type ) | _configuredTypes.Remove( type );

    internal void Remove( HashSet<Type> excludedTypes )
    {
        _allTypes.ExceptWith( excludedTypes );
        foreach( var t in excludedTypes ) _configuredTypes.Remove( t );
    }

    public bool Add( IActivityMonitor monitor, string sourceName, Type type, ConfigurableAutoServiceKind kind )
    {
        if( kind == ConfigurableAutoServiceKind.None )
        {
            return _allTypes.Add( type );
        }
        if( _configuredTypes.AsDictionary.TryGetValue( type, out var exists ) )
        {
            if( exists == kind ) return false;
            monitor.Info( $"{sourceName} updated '{type:N}' from '{exists}' to {kind}." );
        }
        _allTypes.Add( type );
        _configuredTypes.Add( type, kind );
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
        Throw.DebugAssert( set.AllTypes.IsSupersetOf( set._configuredTypes.AsDictionary.Keys ) );
        Throw.DebugAssert( !set._configuredTypes.AsDictionary.Values.Any( k => k == ConfigurableAutoServiceKind.None ) );
    }
}

using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup;

/// <summary>
/// Models the &lt;Types&gt; element.
/// </summary>
public sealed class ExternalTypeConfigurationSet : IReadOnlyCollection<ExternalTypeConfiguration>
{
    readonly Dictionary<Type, ConfigurableAutoServiceKind> _types;

    /// <summary>
    /// Initializes a new empty <see cref="ExternalTypeConfigurationSet"/>.
    /// </summary>
    public ExternalTypeConfigurationSet()
    {
        _types = new Dictionary<Type, ConfigurableAutoServiceKind>();
    }

    /// <summary>
    /// Copy constructor. There must be no duplicate type in <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other set.</param>
    public ExternalTypeConfigurationSet( IReadOnlyCollection<ExternalTypeConfiguration> other )
    {
        Throw.CheckNotNullArgument( other );
        if( other is ExternalTypeConfigurationSet o )
        {
            _types = new Dictionary<Type, ConfigurableAutoServiceKind>( o._types );
        }
        else
        {
            _types = new Dictionary<Type, ConfigurableAutoServiceKind>( other.Count );
            foreach( var tc in other ) _types.Add( tc.Type, tc.Kind );
        }
    }

    internal ExternalTypeConfigurationSet( IEnumerable<XElement> e )
        : this()
    {
        foreach( var tc in e.Elements( EngineConfiguration.xType ).Select( t => new ExternalTypeConfiguration( t ) ) )
        {
            _types.Add( tc.Type, tc.Kind );
        }
    }

    internal XElement ToXml( XName name )
    {
        return new XElement( name,
                             _types.Select( kv => new XElement( EngineConfiguration.xType,
                                                                new XAttribute( EngineConfiguration.xKind, kv.Value ),
                                                                EngineConfiguration.CleanName( kv.Key ) ) ) );
    }

    /// <inheritdoc />
    public int Count => _types.Count;

    /// <summary>
    /// Adds the <see cref="ExternalTypeConfiguration"/>, replacing any existing configuration with the same <see cref="ExternalTypeConfiguration.Type"/>.
    /// </summary>
    /// <param name="configuration">The configuration to add.</param>
    /// <returns>This set.</returns>
    public ExternalTypeConfigurationSet Add( ExternalTypeConfiguration configuration )
    {
        _types[configuration.Type] = configuration.Kind;
        return this;
    }

    /// <summary>
    /// Adds the type association, replacing any existing configuration with the same <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type to configure.</param>
    /// <param name="kind">The kind of the type.</param>
    /// <returns>This set.</returns>
    public ExternalTypeConfigurationSet Add( Type type, ConfigurableAutoServiceKind kind )
    {
        _types[type] = kind;
        return this;
    }

    /// <summary>
    /// Adds that the type whith <see cref="ConfigurableAutoServiceKind.None"/> if it doesn't already
    /// exist but don't change its <see cref="ExternalTypeConfiguration.Kind"/> if it exists.
    /// </summary>
    /// <param name="type">The type to add.</param>
    /// <returns>This set.</returns>
    public ExternalTypeConfigurationSet Add( Type type )
    {
        _types.TryAdd( type, ConfigurableAutoServiceKind.None );
        return this;
    }

    /// <summary>
    /// Gets this set as a dictionary.
    /// </summary>
    public IDictionary<Type, ConfigurableAutoServiceKind> AsDictionary => _types;

    /// <summary>
    /// Merges this set with the other one.
    /// </summary>
    /// <param name="other">The set to merge.</param>
    public void UnionWith( ExternalTypeConfigurationSet other )
    {
        foreach( var kv in other._types )
        {
            _types[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// Removes a configuration.
    /// </summary>
    /// <param name="type">The type to remove.</param>
    /// <returns>True if it has been removed, false if it doesn't exist.</returns>
    public bool Remove( Type type ) => _types.Remove( type );

    /// <summary>
    /// Check if this set contains the same and only the same elements as other.
    /// </summary>
    /// <param name="other">The other set.</param>
    /// <returns>True if this set contains the same configurations as the other one.</returns>
    public bool SetEquals( ExternalTypeConfigurationSet other )
    {
        if( _types.Count == other._types.Count )
        {
            foreach( var kv in _types )
            {
                if( !other._types.TryGetValue( kv.Key, out var oV ) || kv.Value != oV )
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    public struct Enumerator : IEnumerator<ExternalTypeConfiguration>
    {
        // Don't make this readonly or nothing will work!
        Dictionary<Type, ConfigurableAutoServiceKind>.Enumerator _e;

        public Enumerator( Dictionary<Type, ConfigurableAutoServiceKind>.Enumerator e ) => _e = e;

        public ExternalTypeConfiguration Current => new ExternalTypeConfiguration( _e.Current.Key, _e.Current.Value );

        object IEnumerator.Current => Current;

        public void Dispose() => _e.Dispose();

        public bool MoveNext() => _e.MoveNext();

        public void Reset() => _e.MoveNext();
    }

    public Enumerator GetEnumerator() => new Enumerator( _types.GetEnumerator() );

    IEnumerator<ExternalTypeConfiguration> IEnumerable<ExternalTypeConfiguration>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

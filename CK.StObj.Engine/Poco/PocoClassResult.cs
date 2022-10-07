using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class PocoClassResult : IPocoClassSupportResult
        {
            public readonly Dictionary<Type, PocoClassInfo> ByType;
            readonly IReadOnlyDictionary<Type, IPocoClassInfo> _exposedByType;

            public PocoClassResult()
            {
                ByType = new Dictionary<Type, PocoClassInfo>();
                _exposedByType = ByType.AsIReadOnlyDictionary<Type, PocoClassInfo, IPocoClassInfo>();
            }

            IReadOnlyDictionary<Type, IPocoClassInfo> IPocoClassSupportResult.ByType => _exposedByType;

            PocoClassInfo? TryGetClassInfo( IActivityMonitor monitor, Type t )
            {
                Debug.Assert( t != null );
                if( ByType.TryGetValue( t, out var classInfo ) ) return classInfo; 
                if( t.IsInterface )
                {
                    monitor.Error( $"'{t}' is an interface, it cannot be a PocoClass." );
                    return null;
                }
                if( t.IsAbstract )
                {
                    monitor.Error( $"'{t}' is abstract, it cannot be a PocoClass." );
                    return null;
                }
                if( !t.IsClass )
                {
                    monitor.Error( $"'{t}' is not a class, it cannot be a PocoClass." );
                    return null;
                }
                if( t.GetConstructor( Type.EmptyTypes ) == null )
                {
                    monitor.Error( $"'{t}' has no default public constructor, it cannot be a PocoClass." );
                    return null;
                }
                if( !t.GetExternalNames( monitor, out string name, out string[] previousNames ) )
                {
                    return null;
                }
                var c = new PocoClassInfo( t, name, previousNames );
                foreach( var p in t.GetProperties() )
                {
                    if( !p.CanWrite ) continue;
                    var prop = new PocoClassPropertyInfo( p, c.PropertyList.Count );
                    c.Properties.Add( p.Name, prop );
                    c.PropertyList.Add( prop );
                    if( prop.PocoPropertyKind == PocoPropertyKind.PocoClass )
                    {
                        var o = TryGetClassInfo( monitor, prop.PropertyNullableTypeTree.Type );
                    }
                }
            }


            public PocoClassInfo? TryResolveMostSpecified( IActivityMonitor monitor, IEnumerable<(Type Type, string Name)> toCheck )
            {
                var concretes = toCheck.Where( c => !c.Type.IsAbstract && c.Type != typeof( object ) ).ToList();
                for( int i = 0; i < concretes.Count; ++i )
                {
                    for( int j = i+1; j < concretes.Count; ++j )
                    {
                        if( concretes[j].Type.IsAssignableFrom( concretes[i].Type ) )
                        {
                            concretes.RemoveAt( j-- );
                        }
                        else if( concretes[i].Type.IsAssignableFrom( concretes[j].Type ) )
                        {
                            concretes.RemoveAt( i-- );
                            --j;
                        }
                    }
                }
                if( concretes.Count > 1 )
                {
                    monitor.Error( $"Unable to find a common most specialized class for {concretes.Select( c => $"'{c.Type} {c.Name}'" ).Concatenate( " and " )}." );
                    return null;
                }
                var abstracts = toCheck.Where( c => c.Type.IsAbstract && c.Type != typeof( object ) );
                var impl = concretes[0].Type;
                var unimplemented = abstracts.Where( a => !a.Type.IsAssignableFrom( impl ) );
                if( unimplemented.Any() )
                {
                    monitor.Error( $"Abstractions {unimplemented.Select( c => $"'{c.Type} {c.Name}'" ).Concatenate( " and " )} are not implmented by {impl}." );
                    return null;
                }
            }
        }
    }
}

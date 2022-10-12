using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

#nullable enable

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class Result : IPocoSupportResult
        {
            public readonly List<PocoRootInfo> Roots;
            public readonly Dictionary<string, IPocoRootInfo> NamedRoots;
            public readonly Dictionary<Type, InterfaceInfo> AllInterfaces;
            public readonly Dictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces;

            readonly IReadOnlyDictionary<Type, IPocoInterfaceInfo> _exportedAllInterfaces;

            public Result()
            {
                Roots = new List<PocoRootInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedAllInterfaces = AllInterfaces.AsIReadOnlyDictionary<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoRootInfo>>();
                NamedRoots = new Dictionary<string, IPocoRootInfo>();
            }

            IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Roots;

            IReadOnlyDictionary<string, IPocoRootInfo> IPocoSupportResult.NamedRoots => NamedRoots;

            IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => _exportedAllInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => OtherInterfaces;

            /// <summary>
            /// Resolves a set of reference to IPoco to a single family, ensuring that definers or other interfaces
            /// are supported by the family.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="references">References to check.</param>
            /// <returns>The family or null.</returns>
            public PocoRootInfo? TryResolveFamily( IActivityMonitor monitor, IEnumerable<(Type Type, bool Writable, string RefName)> references )
            {
                var families = references.Select( i => (i.Type, i.RefName, i.Writable, Family: AllInterfaces.GetValueOrDefault( i.Type )?.Root) )
                                         .GroupBy( f => f.Family )
                                         .ToList();
                var gAbstracts = families.FirstOrDefault( g => g.Key == null );
                if( gAbstracts != null )
                {
                    families.Remove( gAbstracts );
                }
                if( families.Count == 0 )
                {
                    monitor.Error( @$"No IPoco family found (only ""abstract"" definers or object) for '{references.Select( c => c.RefName ).Concatenate( ", '" )}'." );
                    return null;
                }
                if( families.Count > 1 )
                {
                    var culprits = families.Select( g => $"'{g.Key!.Name}' (for {g.Select( f => f.RefName ).Concatenate()})" ).Concatenate( " and family " );
                    monitor.Error( $"All IPoco must belong to the same IPoco family, found family {culprits}." );
                    return null;
                }
                var f = families[0].Key;
                Debug.Assert( f != null );
                bool success = true;
                if( gAbstracts != null )
                {
                    if( !f.IsClosedPoco )
                    {
                        var closed = gAbstracts.FirstOrDefault( g => g.Type == typeof( IClosedPoco ) );
                        if( closed.Type != null )
                        {
                            monitor.Error( $"'{closed.RefName}' is IClosedPoco but '{f.Name}' is not a closed Poco." );
                            success = false;
                        }
                    }
                    var disallowed = gAbstracts.Where( a => a.Writable );
                    if( disallowed.Any() )
                    {
                        success = false;
                        foreach( var d in disallowed )
                        {
                            monitor.Error( @$"Property '{d.Type} {d.RefName}' is writable, it must be one the '{f.Name}' interfaces: {f.Interfaces.Select( i => i.PocoInterface.ToCSharpName()).Concatenate()}." );
                        }
                    }
                    var abstracts = gAbstracts.Where( a => a.Type != typeof( IPoco ) && a.Type != typeof(object) && a.Type != typeof( IClosedPoco ) );
                    var unimplemented = abstracts.Where( a => !f.OtherInterfaces.Contains( a.Type ) );
                    if( unimplemented.Any() )
                    {
                        monitor.Error( $"Types '{unimplemented.Select( u => $"{u.Type} {u.RefName}" ).Concatenate( "' ,'" )}' are not supported by '{f.Name}'." );
                        success = false;
                    }
                }
                return success ? f : null;
            }

            public bool Conclude( IActivityMonitor monitor )
            {
                List<PropertyInfo>? clashPath = null;
                foreach( var c in Roots )
                {
                    if( !c.Conclude( monitor, this ) )
                    {
                        return false;
                    }
                    if( c.HasCycle( monitor, AllInterfaces, ref clashPath ) ) break;
                }
                if( clashPath != null )
                {
                    if( clashPath.Count > 0 )
                    {
                        clashPath.Reverse();
                        monitor.Error( $"Poco readonly property cycle detected: '{clashPath.Select( p => $"{p.DeclaringType!.FullName}.{p.Name}" ).Concatenate( "' -> '" )}." );
                    }
                    return false;
                }
                return true;
            }

            public bool CheckPropertiesVarianceAndInstantiationCycleError( IActivityMonitor monitor )
            {
                List<PropertyInfo>? clashPath = null;
                foreach( var c in Roots )
                {
                    if( !c.Conclude( monitor, this ) )
                    {
                        return false;
                    }

                    if( !c.CheckPropertiesVarianceAndUnionTypes( monitor, AllInterfaces ) ) return false;
                    if( c.HasCycle( monitor, AllInterfaces, ref clashPath ) ) break;
                }
                if( clashPath != null )
                {
                    if( clashPath.Count > 0 )
                    {
                        clashPath.Reverse();
                        monitor.Error( $"Poco readonly property cycle detected: '{clashPath.Select( p => $"{p.DeclaringType!.FullName}.{p.Name}" ).Concatenate( "' -> '" )}." );
                    }
                    return false;
                }
                return true;
            }

            public bool BuildNameIndex( IActivityMonitor monitor )
            {
                bool success = true;
                foreach( var r in Roots )
                {
                    foreach( var name in r.PreviousNames.Append( r.Name ) )
                    {
                        if( NamedRoots.TryGetValue( name, out var exists ) )
                        {
                            monitor.Error( $"The Poco name '{name}' clashes: both '{r.Interfaces[0].PocoInterface.AssemblyQualifiedName}' and '{exists.Interfaces[0].PocoInterface.AssemblyQualifiedName}' share it." );
                            success = false;
                        }
                        else NamedRoots.Add( name, r );
                    }
                }
                return success;
            }

            public bool IsAssignableFrom( IPocoPropertyInfo target, IPocoPropertyInfo from )
            {
                if( from == null ) throw new ArgumentNullException( nameof( from ) );
                if( target == null ) throw new ArgumentNullException( nameof( target ) );
                if( target == from ) return true;
                if( from.PropertyUnionTypes.Any() )
                {
                    foreach( var f in from.PropertyUnionTypes )
                    {
                        if( !IsAssignableFrom( target, f.Type, f.Kind ) ) return false;

                    }
                    return true;
                }
                return IsAssignableFrom( target, from.PropertyType, from.PropertyNullableTypeTree.Kind );
            }

            public bool IsAssignableFrom( IPocoPropertyInfo target, Type from, NullabilityTypeKind fromNullability )
            {
                if( from == null ) throw new ArgumentNullException( nameof( from ) );
                if( target == null ) throw new ArgumentNullException( nameof( target ) );
                if( target.PropertyUnionTypes.Any() )
                {
                    foreach( var t in target.PropertyUnionTypes )
                    {
                        if( IsAssignableFrom( t.Type, t.Kind, from, fromNullability ) ) return true;
                    }
                    return false;
                }
                return IsAssignableFrom( target.PropertyType, target.PropertyNullableTypeTree.Kind, from, fromNullability );
            }

            public bool IsAssignableFrom( Type target, NullabilityTypeKind targetNullability, Type from, NullabilityTypeKind fromNullability )
            {
                if( from == null ) throw new ArgumentNullException( nameof( from ) );
                if( target == null ) throw new ArgumentNullException( nameof( target ) );
                // A non nullable cannot be assigned from a nullable.
                if( !targetNullability.IsNullable() && fromNullability.IsNullable() )
                {
                    return false;
                }
                return target == from
                        || target.IsAssignableFrom( from )
                        || (AllInterfaces.TryGetValue( target, out var tP )
                            && AllInterfaces.TryGetValue( from, out var fP )
                            && tP.Root == fP.Root);
            }

        }
    }
}

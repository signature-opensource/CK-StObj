using CK.CodeGen;
using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            readonly IReadOnlyDictionary<Type, IPocoInterfaceInfo> _exportedAllInterfaces;
            public readonly List<PocoRootInfo> Roots;
            public readonly Dictionary<Type, InterfaceInfo> AllInterfaces;
            public readonly Dictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces;
            public readonly Dictionary<string, IPocoRootInfo> NamedRoots;
            public readonly PocoLikeResult PocoLike;

            public Result()
            {
                Roots = new List<PocoRootInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedAllInterfaces = AllInterfaces.AsIReadOnlyDictionary<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoRootInfo>>();
                NamedRoots = new Dictionary<string, IPocoRootInfo>();
                PocoLike = new PocoLikeResult();
            }

            IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Roots;

            IReadOnlyDictionary<string, IPocoRootInfo> IPocoSupportResult.NamedRoots => NamedRoots;

            IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => _exportedAllInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => OtherInterfaces;

            IPocoLikeSupportResult IPocoSupportResult.PocoLike => PocoLike;

            public bool CheckPropertiesVarianceAndInstantiationCycleError( IActivityMonitor monitor )
            {
                List<PropertyInfo>? clashPath = null;
                foreach( var c in Roots )
                {
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

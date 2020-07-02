using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

#nullable enable

namespace CK.Setup
{
    partial class PocoRegisterer
    {
        class Result : IPocoSupportResult
        {
            readonly IReadOnlyDictionary<Type, IPocoInterfaceInfo> _exportedInterfaces;
            public readonly List<ClassInfo> Roots;
            public readonly Dictionary<Type, InterfaceInfo> AllInterfaces;
            public readonly Dictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces;
            public readonly Dictionary<string, IPocoRootInfo> NamedRoots;

            public Result()
            {
                Roots = new List<ClassInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedInterfaces = AllInterfaces.AsCovariantReadOnly<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoRootInfo>>();
                NamedRoots = new Dictionary<string, IPocoRootInfo>();
            }

            IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Roots;

            IReadOnlyDictionary<string, IPocoRootInfo> IPocoSupportResult.NamedRoots => NamedRoots;

            IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => _exportedInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => OtherInterfaces;

            public bool HasInstantiationCycle( IActivityMonitor monitor )
            {
                List<PropertyInfo>? clashPath = null;
                foreach( var c in Roots )
                {
                    if( !c.CheckPropertiesVariance( monitor, AllInterfaces ) ) return false;
                    if( c.HasCycle( monitor, AllInterfaces, ref clashPath ) ) break;
                }
                if( clashPath != null )
                {
                    clashPath.Reverse();
                    monitor.Error( $"Auto instantiable Poco property cycle detected: '{clashPath.Select( p => $"{p.DeclaringType!.FullName}.{p.Name}" ).Concatenate( "' -> '" )}." );
                    return true;
                }
                return false;
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
        }

        class ClassInfo : IPocoRootInfo
        {
            public Type PocoClass { get; }
            public Type PocoFactoryClass { get; }
            public string Name { get; set; }
            public IReadOnlyList<string> PreviousNames { get; set; }
            public Type? ClosureInterface { get; }
            public bool IsClosedPoco { get; }
            public readonly List<InterfaceInfo> Interfaces;
            public HashSet<Type> OtherInterfaces;

            public Dictionary<string, PocoPropertyInfo> Properties;
            IReadOnlyDictionary<string, IPocoPropertyInfo> _exposedProperties;
            public IReadOnlyList<PocoPropertyInfo> PropertyList { get; }

            IReadOnlyDictionary<string, IPocoPropertyInfo> IPocoRootInfo.Properties => _exposedProperties;
            IReadOnlyList<IPocoPropertyInfo> IPocoRootInfo.PropertyList => PropertyList;


            IReadOnlyList<IPocoInterfaceInfo> IPocoRootInfo.Interfaces => Interfaces;
            IReadOnlyCollection<Type> IPocoRootInfo.OtherInterfaces => OtherInterfaces;

            bool _instantiationCycleDone;
            bool _instantiationCycleFlag;

            public ClassInfo( Type pocoClass,
                              Type pocoFactoryClass,
                              bool mustBeClosed,
                              Type? closureInterface,
                              HashSet<Type> others,
                              Dictionary<string, PocoPropertyInfo> properties,
                              IReadOnlyList<PocoPropertyInfo> propertyList )
            {
                PocoClass = pocoClass;
                PocoFactoryClass = pocoFactoryClass;
                ClosureInterface = closureInterface;
                IsClosedPoco = mustBeClosed;
                Interfaces = new List<InterfaceInfo>();
                OtherInterfaces = others;
                Properties = properties;
                _exposedProperties = Properties.AsCovariantReadOnly<string, PocoPropertyInfo, IPocoPropertyInfo>();
                PropertyList = propertyList;
            }

            /// <summary>
            /// Checks that for each property definition with the same name, the return type is
            /// either NOT co nor contravariant (general case), or BOTH co and contravariant (for
            /// IPoco types).
            /// There may be an evolution here: covariance may be accepted as long as base properties
            /// do not expose a setter... 
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="allInterfaces">The interfaces indexes.</param>
            /// <returns>True on success, false on error.</returns>
            internal bool CheckPropertiesVariance( IActivityMonitor monitor, Dictionary<Type, InterfaceInfo> allInterfaces )
            {
                foreach( var p in PropertyList )
                {
                    var refType = p.PropertyType;
                    foreach( var other in p.DeclaredProperties.Skip( 1 ) )
                    {
                        bool isSameOrPocoFamily = refType == other.PropertyType
                                                    || (allInterfaces.TryGetValue( refType, out var i1 )
                                                       && allInterfaces.TryGetValue( other.PropertyType, out var i2 )
                                                       && i1.Root == i2.Root);
                        if( !isSameOrPocoFamily )
                        {
                            monitor.Error( $"Interface '{p.DeclaredProperties[0].DeclaringType}' and '{other.DeclaringType!.FullName}' both declare property '{p.PropertyName}' but their type differ ({p.PropertyType.Name} vs. {other.PropertyType.Name})." );
                            return false;
                        }
                    }
                }
                return true;
            }

            internal bool HasCycle( IActivityMonitor monitor, Dictionary<Type, InterfaceInfo> allInterfaces, ref List<PropertyInfo>? clashPath )
            {
                if( _instantiationCycleFlag ) return true;
                if( _instantiationCycleDone ) return false;
                _instantiationCycleDone = true;
                _instantiationCycleFlag = true;

                var createdPocos = PropertyList.Where( p => p.AutoInstantiated && typeof( IPoco ).IsAssignableFrom( p.PropertyType ) );
                if( createdPocos.Any() )
                {
                    HashSet<ClassInfo>? classes = null;
                    foreach( var p in createdPocos )
                    {
                        if( !allInterfaces.TryGetValue( p.PropertyType, out InterfaceInfo? target ) )
                        {
                            // The IPoco interface type is NOT registered as a IPoco.
                            // This MAY be possible: we don't consider this to be a IPoco.
                            monitor.Warn( $"Auto instantiable Poco property '{p.PropertyType.DeclaringType!.FullName}.{p.PropertyName}': the property type {p.PropertyType.Name} is not registered. This property will be a read only property initialized to null." );
                        }
                        else
                        {
                            if( classes == null ) classes = new HashSet<ClassInfo>();
                            if( classes.Add( target.Root ) )
                            {
                                if( target.Root.HasCycle( monitor, allInterfaces, ref clashPath ) )
                                {
                                    if( clashPath == null ) clashPath = new List<PropertyInfo>();
                                    clashPath.Add( p.DeclaredProperties[0] );
                                    _instantiationCycleFlag = false;
                                    return true;
                                }
                            }
                        }
                    }
                }
                _instantiationCycleFlag = false;
                return false;
            }

            public override string ToString() => $"Poco: {Interfaces[0].PocoInterface.FullName} ({Interfaces.Count} interfaces)";

            public bool InitializeNames( IActivityMonitor monitor )
            {
                string name;
                string[] previousNames;

                var primary = Interfaces[0].PocoInterface;
                var names = primary.GetCustomAttributesData().Where( d => typeof( PocoNameAttribute ).IsAssignableFrom( d.AttributeType ) ).FirstOrDefault();

                var others = Interfaces.Where( i => i.PocoInterface != primary
                                                    && i.PocoInterface.GetCustomAttributesData().Any( x => typeof( PocoNameAttribute ).IsAssignableFrom( x.AttributeType ) ) );
                if( others.Any() )
                {
                    monitor.Error( $"PocoName attribute appear on '{others.Select( i => i.PocoInterface.FullName ).Concatenate( "', '" )}'. Only the primary IPoco interface (i.e. '{primary.FullName}') should define the Poco names." );
                    return false;
                }
                if( names != null )
                {
                    var args = names.ConstructorArguments;
                    name = (string)args[0].Value!;
                    previousNames = ((IEnumerable<CustomAttributeTypedArgument>)args[1].Value!).Select( a => (string)a.Value! ).ToArray();
                    if( String.IsNullOrWhiteSpace( name ) )
                    {
                        monitor.Error( $"Empty name in PocoName attribute on '{primary.FullName}'." );
                        return false;
                    }
                    if( previousNames.Any( n => String.IsNullOrWhiteSpace( n ) ) )
                    {
                        monitor.Error( $"Empty previous name in PocoName attribute on '{primary.FullName}'." );
                        return false;
                    }
                    if( previousNames.Contains( name ) || previousNames.GroupBy( Util.FuncIdentity ).Any( g => g.Count() > 1 ) )
                    {
                        monitor.Error( $"Duplicate PocoName in attribute on '{primary.FullName}'." );
                        return false;
                    }
                }
                else
                {
                    name = primary.FullName!;
                    previousNames = Array.Empty<string>();
                    monitor.Warn( $"Poco '{name}' use its full name as its name since no [PocoName] attribute is defined." );
                }
                Name = name;
                PreviousNames = previousNames;
                return true;
            }
        }

        class InterfaceInfo : IPocoInterfaceInfo
        {
            public readonly ClassInfo Root;
            public Type PocoInterface { get; }
            public Type PocoFactoryInterface { get; }

            IPocoRootInfo IPocoInterfaceInfo.Root => Root;

            public InterfaceInfo( ClassInfo root, Type pocoInterface, Type pocoFactoryInterface )
            {
                Root = root;
                PocoInterface = pocoInterface;
                PocoFactoryInterface = pocoFactoryInterface;
            }
        }

        class PocoPropertyInfo : IPocoPropertyInfo
        {
            public bool AutoInstantiated { get; set; }

            public bool HasDeclaredSetter { get; set; }

            public bool Setter { get; set; }

            public string? DefaultValueSource { get; set; }

            public Type PropertyType => DeclaredProperties[0].PropertyType;

            public string PropertyName => DeclaredProperties[0].Name;

            public List<PropertyInfo> DeclaredProperties { get; }

            IReadOnlyList<PropertyInfo> IPocoPropertyInfo.DeclaredProperties => DeclaredProperties;

            public PocoPropertyInfo( PropertyInfo first )
            {
                DeclaredProperties = new List<PropertyInfo>() { first };
            }


            public override string ToString() => $"Property '{PropertyName}' of type '{PropertyType.Name}' on interfaces: '{DeclaredProperties.Select( p => p.DeclaringType!.FullName ).Concatenate("', '")}'.";
        }
    }
}

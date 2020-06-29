using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

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
            public Type? FinalFactory;

            // Exposed FinalFactory is necessarily not null.
            Type IPocoSupportResult.FinalFactory => FinalFactory!;

            public Result()
            {
                Roots = new List<ClassInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedInterfaces = AllInterfaces.AsCovariantReadOnly<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoRootInfo>>();
            }

            IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Roots;

            IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => _exportedInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => OtherInterfaces;

            public bool HasInstantiationCycle( IActivityMonitor monitor )
            {
                List<PropertyInfo>? clashPath = null;
                foreach( var c in Roots )
                {
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
        }

        class ClassInfo : IPocoRootInfo
        {
            public Type PocoClass { get; }
            public Type? ClosureInterface { get; }
            public bool IsClosedPoco { get; }
            public MethodBuilder StaticMethod;
            public readonly List<InterfaceInfo> Interfaces;
            public HashSet<Type> OtherInterfaces;

            public Dictionary<string, PocoPropertyInfo> Properties;
            IReadOnlyDictionary<string, IPocoPropertyInfo> _exposedProperties;
            public IReadOnlyList<PocoPropertyInfo> PropertyList { get; }

            IReadOnlyDictionary<string, IPocoPropertyInfo> IPocoRootInfo.Properties => _exposedProperties;
            IReadOnlyList<IPocoPropertyInfo> IPocoRootInfo.PropertyList => PropertyList;


            IReadOnlyList<IPocoInterfaceInfo> IPocoRootInfo.Interfaces => Interfaces;
            IReadOnlyCollection<Type> IPocoRootInfo.OtherInterfaces => OtherInterfaces;

            public ClassInfo( Type pocoClass,
                              bool mustBeClosed,
                              Type? closureInterface,
                              HashSet<Type> others,
                              Dictionary<string, PocoPropertyInfo> properties,
                              IReadOnlyList<PocoPropertyInfo> propertyList )
            {
                PocoClass = pocoClass;
                ClosureInterface = closureInterface;
                IsClosedPoco = mustBeClosed;
                Interfaces = new List<InterfaceInfo>();
                OtherInterfaces = others;
                Properties = properties;
                _exposedProperties = Properties.AsCovariantReadOnly<string, PocoPropertyInfo, IPocoPropertyInfo>();
                PropertyList = propertyList;
            }

            bool _instantiationCycleDone;
            bool _instantiationCycleFlag;

            internal bool HasCycle( IActivityMonitor monitor, Dictionary<Type, InterfaceInfo> allInterfaces, ref List<PropertyInfo>? clashPath )
            {
                if( _instantiationCycleFlag ) return true;
                if( _instantiationCycleDone ) return false;
                _instantiationCycleDone = true;
                _instantiationCycleFlag = true;
                var createdPocos = Properties.Values.Where( p => p.AutoInstantiated && typeof( IPoco ).IsAssignableFrom( p.PropertyType ) );
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

using CK.CodeGen;
using CK.Core;
using CK.Text;
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

            public bool CheckPropertiesVarianceAndInstantiationCycle( IActivityMonitor monitor )
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
            AnnotationSetImpl _annotations;

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

            public IReadOnlyList<PropertyInfo> ExternallyImplementedPropertyList { get; }

            bool _instantiationCycleDone;
            bool _instantiationCycleFlag;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
            public ClassInfo( Type pocoClass,
                              Type pocoFactoryClass,
                              bool mustBeClosed,
                              Type? closureInterface,
                              HashSet<Type> others,
                              Dictionary<string, PocoPropertyInfo> properties,
                              IReadOnlyList<PocoPropertyInfo> propertyList,
                              IReadOnlyList<PropertyInfo>? externallyImplementedPropertyList )
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
                ExternallyImplementedPropertyList = externallyImplementedPropertyList ?? Array.Empty<PropertyInfo>();
            }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

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
                        // Types are equal but NRT must be checked.
                        var otherN = other.GetNullabilityInfo();
                        if( !otherN.Equals( p.PropertyNullabilityInfo ) )
                        {
                            monitor.Error( $"Interface '{p.DeclaredProperties[0].DeclaringType}' and '{other.DeclaringType!.FullName}' both declare property '{p.PropertyName}' with the same type {p.PropertyType.ToCSharpName()} but their type's Nullabilty differ." );
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
                            p.AutoInstantiated = false;
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
                Type primary = Interfaces[0].PocoInterface;
                if( !primary.GetExternalNames( monitor, out string name, out string[] previousNames ) )
                {
                    return false;
                }
                var others = Interfaces.Where( i => i.PocoInterface != primary
                                                    && i.PocoInterface.GetCustomAttributesData().Any( x => typeof( ExternalNameAttribute ).IsAssignableFrom( x.AttributeType ) ) );
                if( others.Any() )
                {
                    monitor.Error( $"ExternalName attribute appear on '{others.Select( i => i.PocoInterface.FullName ).Concatenate( "', '" )}'. Only the primary IPoco interface (i.e. '{primary.FullName}') should define the Poco names." );
                    return false;
                }
                Name = name;
                PreviousNames = previousNames;
                return true;
            }

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();
        }

        class InterfaceInfo : IPocoInterfaceInfo
        {
            public readonly ClassInfo Root;
            public Type PocoInterface { get; }
            public Type PocoFactoryInterface { get; }

            AnnotationSetImpl _annotations;

            IPocoRootInfo IPocoInterfaceInfo.Root => Root;

            public InterfaceInfo( ClassInfo root, Type pocoInterface, Type pocoFactoryInterface )
            {
                Root = root;
                PocoInterface = pocoInterface;
                PocoFactoryInterface = pocoFactoryInterface;
            }

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        }

        class PocoPropertyInfo : IPocoPropertyInfo
        {
            Dictionary<Type,NullabilityTypeKind>? _unionTypes;
            NullableTypeTree _nullableTypeTree;
            AnnotationSetImpl _annotations;

            public bool AutoInstantiated { get; set; }

            public bool HasDeclaredSetter { get; set; }

            public bool Setter { get; set; }

            public bool HasDefaultValue { get; set; }

            public object? DefaultValue { get; set; }

            public string? DefaultValueSource { get; set; }

            public int Index { get; set; }

            public NullabilityTypeInfo PropertyNullabilityInfo { get; set; }

            public NullableTypeTree PropertyNullableTypeTree => _nullableTypeTree.Kind == NullabilityTypeKind.Unknown
                                                                    ? (_nullableTypeTree = PropertyType.GetNullableTypeTree( PropertyNullabilityInfo ))
                                                                    : _nullableTypeTree;

            public Type PropertyType => DeclaredProperties[0].PropertyType;

            public IEnumerable<(Type Type,NullabilityTypeKind Kind)> PropertyUnionTypes => _unionTypes != null
                                                                                    ? _unionTypes.Select( kv => (kv.Key,kv.Value) )
                                                                                    : Enumerable.Empty<(Type,NullabilityTypeKind)>();
            public bool IsEventuallyNullable => PropertyUnionTypes.Any()
                                                    ? PropertyUnionTypes.Any( x => x.Kind.IsNullable() )
                                                    : PropertyNullabilityInfo.Kind.IsNullable();

            public string PropertyName => DeclaredProperties[0].Name;

            public List<PropertyInfo> DeclaredProperties { get; }

            IReadOnlyList<PropertyInfo> IPocoPropertyInfo.DeclaredProperties => DeclaredProperties;

            public PocoPropertyInfo( PropertyInfo first, int index )
            {
                DeclaredProperties = new List<PropertyInfo>() { first };
                Index = index;
            }

            public void AddUnionPropertyTypes( IReadOnlyList<Type> types )
            {
                Debug.Assert( types.Count > 0 );
                if( _unionTypes == null ) _unionTypes = new Dictionary<Type, NullabilityTypeKind>();
                // Different Poco can (re)define variants.
                foreach( var t in types )
                {
                    _unionTypes[t] = t.GetNullabilityKind();
                }
            }

            public override string ToString() => $"Property '{PropertyName}' of type '{PropertyType.Name}' on interfaces: '{DeclaredProperties.Select( p => p.DeclaringType!.FullName ).Concatenate("', '")}'.";

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();
        }
    }
}

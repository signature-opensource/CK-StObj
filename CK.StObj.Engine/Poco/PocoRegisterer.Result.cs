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
            readonly IReadOnlyDictionary<Type, IPocoInterfaceInfo> _exportedAllInterfaces;
            public readonly List<ClassInfo> Roots;
            public readonly Dictionary<Type, InterfaceInfo> AllInterfaces;
            public readonly Dictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces;
            public readonly Dictionary<string, IPocoRootInfo> NamedRoots;

            public Result()
            {
                Roots = new List<ClassInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedAllInterfaces = AllInterfaces.AsCovariantReadOnly<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoRootInfo>>();
                NamedRoots = new Dictionary<string, IPocoRootInfo>();
            }

            IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Roots;

            IReadOnlyDictionary<string, IPocoRootInfo> IPocoSupportResult.NamedRoots => NamedRoots;

            IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => _exportedAllInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => OtherInterfaces;

            public bool CheckPropertiesVarianceAndInstantiationCycleError( IActivityMonitor monitor )
            {
                List<PropertyInfo>? clashPath = null;
                foreach( var c in Roots )
                {
                    if( !c.CheckPropertiesVariance( monitor, AllInterfaces ) ) return false;
                    if( c.HasCycle( monitor, AllInterfaces, ref clashPath ) ) break;
                }
                if( clashPath != null )
                {
                    if( clashPath.Count > 0 )
                    {
                        clashPath.Reverse();
                        monitor.Error( $"Poco readonly property cycle detected: '{clashPath.Select( p => $"{p.DeclaringType!.FullName}.{p.Name}" ).Concatenate( "' -> '" )}." );
                    }
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
                return IsAssignableFrom( target, from.PropertyType, from.PropertyNullabilityInfo.Kind );
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
                return IsAssignableFrom( target.PropertyType, target.PropertyNullabilityInfo.Kind, from, fromNullability );
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
                    var refType = p.PropertyNullableTypeTree;
                    foreach( var other in p.DeclaredProperties.Skip( 1 ) )
                    {
                        bool isSameOrPocoFamily = refType.Type == other.PropertyType
                                                    || (allInterfaces.TryGetValue( refType.Type, out var i1 )
                                                       && allInterfaces.TryGetValue( other.PropertyType, out var i2 )
                                                       && i1.Root == i2.Root);
                        if( !isSameOrPocoFamily )
                        {
                            monitor.Error( $"Interface '{p.DeclaredProperties[0].DeclaringType}' and '{other.DeclaringType!.FullName}' both declare property '{p.PropertyName}' but their type differ ('{refType}' vs. '{other.GetNullableTypeTree()}')." );
                            return false;
                        }
                        // Types are equal but NRT must be checked.
                        var otherN = other.GetNullabilityInfo();
                        if( !otherN.Equals( p.PropertyNullabilityInfo ) )
                        {
                            monitor.Error( $"Interface '{p.DeclaredProperties[0].DeclaringType}' and '{other.DeclaringType!.FullName}' both declare property '{p.PropertyName}' with the same type but their nullability differ ('{refType}' vs. '{other.PropertyType.GetNullableTypeTree( otherN )}')." );
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

                var createdPocos = PropertyList.Where( p => p.IsReadOnly && typeof( IPoco ).IsAssignableFrom( p.PropertyType ) );
                if( createdPocos.Any() )
                {
                    HashSet<ClassInfo>? classes = null;
                    foreach( var p in createdPocos )
                    {
                        if( !allInterfaces.TryGetValue( p.PropertyType, out InterfaceInfo? target ) )
                        {
                            // The IPoco interface type is NOT registered as a IPoco.
                            // We fail on this (we have no way to create the instance).
                            monitor.Error( $"Poco readonly property '{p.PropertyType.DeclaringType!.FullName}.{p.PropertyName}': the property type {p.PropertyType.Name} is a IPoco that is not registered." );
                            // Trick: we instantiate an empty clashPath to signal this case: a resulting
                            // empty path is an unregistered IPoco (and error has been logged).
                            clashPath = new List<PropertyInfo>();
                            return false;
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
            NullableTypeTree _nullableTypeTree;
            AnnotationSetImpl _annotations;
            IReadOnlyList<NullableTypeTree>? _unionTypes;
            bool _unionTypesCanBeExtended;

            public bool IsReadOnly { get; set; }

            public bool HasDefaultValue { get; set; }

            public object? DefaultValue { get; set; }

            public string? DefaultValueSource { get; set; }

            public int Index { get; set; }

            public NullabilityTypeInfo PropertyNullabilityInfo { get; set; }

            public bool IsNullable => PropertyNullabilityInfo.Kind.IsNullable();

            public NullableTypeTree PropertyNullableTypeTree => _nullableTypeTree.Kind == NullabilityTypeKind.Unknown
                                                                    ? (_nullableTypeTree = PropertyType.GetNullableTypeTree( PropertyNullabilityInfo ))
                                                                    : _nullableTypeTree;

            public Type PropertyType => DeclaredProperties[0].PropertyType;

            public IEnumerable<NullableTypeTree> PropertyUnionTypes => _unionTypes != null
                                                                            ? _unionTypes
                                                                            : Enumerable.Empty<NullableTypeTree>();

            public string PropertyName => DeclaredProperties[0].Name;

            public List<PropertyInfo> DeclaredProperties { get; }

            IReadOnlyList<PropertyInfo> IPocoPropertyInfo.DeclaredProperties => DeclaredProperties;

            public PocoPropertyInfo( PropertyInfo first, int index )
            {
                DeclaredProperties = new List<PropertyInfo>() { first };
                Index = index;
            }

            public bool AddUnionPropertyTypes( IActivityMonitor monitor, IReadOnlyList<NullableTypeTree> types, bool typesCanBeExtended )
            {
                Debug.Assert( types.Count > 0 );
                if( _unionTypes == null )
                {
                    _unionTypes = types;
                    _unionTypesCanBeExtended = typesCanBeExtended;
                    return true;
                }
                bool success = true;
                List<NullableTypeTree>? extended = null;
                foreach( var t in types )
                {
                    if( !_unionTypes.Contains( t ) )
                    {
                        if( !_unionTypesCanBeExtended )
                        {
                            monitor.Error( $"Existing union type cannot be extended. Type '{t}' is a new one (existing types are: '{_unionTypes.Select( t => t.ToString() ).Concatenate( "', '" )}')." );
                            success = false;
                        }
                        else
                        {
                            if( extended == null ) extended = _unionTypes as List<NullableTypeTree> ?? new List<NullableTypeTree>( _unionTypes );
                            extended.Add( t );
                        }
                    }
                }
                if( success )
                {
                    if( !typesCanBeExtended )
                    {
                        _unionTypesCanBeExtended = false;
                        foreach( var t in _unionTypes )
                        {
                            if( !types.Contains( t ) )
                            {
                                monitor.Error( $"Current union type definition cannot be extended. Existing union type defines type '{t}' that is not defined by these union types '{types.Select( t => t.ToString() ).Concatenate( "', '" )}')." );
                                success = false;
                            }
                        }
                    }
                    if( success && extended != null )
                    {
                        _unionTypes = extended;
                    }
                }
                return success;
            }

            public override string ToString() => $"Property '{PropertyName}' of type '{PropertyType.Name}' on interfaces: '{DeclaredProperties.Select( p => p.DeclaringType!.FullName ).Concatenate( "', '" )}'.";

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

using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

#nullable enable

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class PocoRootInfo : IPocoRootInfo
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

            public readonly Dictionary<string, PocoPropertyInfo> Properties;
            readonly IReadOnlyDictionary<string, IPocoPropertyInfo> _exposedProperties;
            public IReadOnlyList<PocoPropertyInfo> PropertyList { get; }

            IReadOnlyDictionary<string, IPocoPropertyInfo> IPocoRootInfo.Properties => _exposedProperties;
            IReadOnlyList<IPocoPropertyInfo> IPocoRootInfo.PropertyList => PropertyList;


            IReadOnlyList<IPocoInterfaceInfo> IPocoRootInfo.Interfaces => Interfaces;
            IReadOnlyCollection<Type> IPocoRootInfo.OtherInterfaces => OtherInterfaces;

            public IReadOnlyList<PropertyInfo> ExternallyImplementedPropertyList { get; }

            bool _instantiationCycleDone;
            bool _instantiationCycleFlag;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
            public PocoRootInfo( Type pocoClass,
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
                _exposedProperties = Properties.AsIReadOnlyDictionary<string, PocoPropertyInfo, IPocoPropertyInfo>();
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
            internal bool CheckPropertiesVarianceAndUnionTypes( IActivityMonitor monitor, Dictionary<Type, InterfaceInfo> allInterfaces )
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
                            monitor.Error( $"Interface '{p.DeclaredProperties[0].DeclaringType.ToCSharpName()}' and '{other.DeclaringType!.ToCSharpName()}' both declare property '{p.PropertyName}' but their type differ ('{refType}' vs. '{other.GetNullableTypeTree()}')." );
                            return false;
                        }
                        // Types are equal but NRT must be checked.
                        var otherN = other.GetNullabilityInfo();
                        if( !otherN.Equals( p.NullabilityTypeInfo ) )
                        {
                            monitor.Error( $"Interface '{p.DeclaredProperties[0].DeclaringType}' and '{other.DeclaringType!.FullName}' both declare property '{p.PropertyName}' with the same type but their nullability differ ('{refType}' vs. '{other.PropertyType.GetNullableTypeTree( otherN )}')." );
                            return false;
                        }
                    }
                    if( p.PropertyUnionTypes.Any() )
                    {
                        if( !p.OptimizeUnionTypes( monitor ) ) return false;
                        Debug.Assert( p.PropertyUnionTypes.Select( nt => nt.Type ).GroupBy( Util.FuncIdentity ).Count( g => g.Count() > 1 ) == 0,
                                      "There must be NO actual type duplicates considering the Union optimization rules." );
                    }
                }
                return true;
            }

            // Since Poco-like are not allowed to be readonly properties, we don't handle them here.
            // We focus only on IPoco.
            internal bool HasCycle( IActivityMonitor monitor, Dictionary<Type, InterfaceInfo> allInterfaces, ref List<PropertyInfo>? clashPath )
            {
                if( _instantiationCycleFlag ) return true;
                if( _instantiationCycleDone ) return false;
                _instantiationCycleDone = true;
                _instantiationCycleFlag = true;

                var createdPocos = PropertyList.Where( p => p.IsReadOnly && typeof( IPoco ).IsAssignableFrom( p.PropertyType ) );
                if( createdPocos.Any() )
                {
                    HashSet<PocoRootInfo>? classes = null;
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
                            if( classes == null ) classes = new HashSet<PocoRootInfo>();
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
                    monitor.Error( $"ExternalName attribute appear on '{others.Select( i => i.PocoInterface.ToCSharpName() ).Concatenate( "', '" )}'. Only the primary IPoco interface (i.e. '{primary.ToCSharpName()}') should define the Poco names." );
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
    }
}

using CK.CodeGen;
using CK.Core;
using CK.Reflection;
using CK.Setup;
using CK.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Registerer for <see cref="IPoco"/> interfaces.
    /// </summary>
    partial class PocoRegisterer
    {
        class PocoType
        {
            public readonly Type Type;
            public readonly PocoType Root;
            public readonly List<Type>? RootCollector;

            public PocoType( Type type, PocoType? root )
            {
                Type = type;
                if( root != null )
                {
                    Debug.Assert( root.RootCollector != null, "The root centralizes the collector." );
                    Root = root;
                    root.RootCollector.Add( type );
                }
                else
                {
                    Root = this;
                    RootCollector = new List<Type>() { type };
                }
            }
        }

        readonly Dictionary<Type, PocoType?> _all;
        readonly List<List<Type>> _result;
        readonly string _namespace;
        readonly Func<IActivityMonitor, Type, bool> _typeFilter;
        readonly Func<IActivityMonitor, Type, bool> _actualPocoPredicate;

        /// <summary>
        /// Initializes a new <see cref="PocoRegisterer"/>.
        /// </summary>
        /// <param name="actualPocoPredicate">
        /// This must be true for actual IPoco interfaces: when false, "base interface" are not directly registered.
        /// This implements the <see cref="CKTypeDefinerAttribute"/> behavior.
        /// </param>
        /// <param name="namespace">Namespace into which dynamic types will be created.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        public PocoRegisterer( Func<IActivityMonitor, Type, bool> actualPocoPredicate, string @namespace = "CK.GPoco", Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            _actualPocoPredicate = actualPocoPredicate ?? throw new ArgumentNullException( nameof( actualPocoPredicate ) );
            _namespace = @namespace ?? "CK.GPoco";
            _all = new Dictionary<Type, PocoType?>();
            _result = new List<List<Type>>();
            _typeFilter = typeFilter ?? (( m, type ) => true);
        }

        /// <summary>
        /// Registers a type that may be a <see cref="IPoco"/> interface.
        /// </summary>
        /// <param name="monitor">Monitor that will be used to signal errors.</param>
        /// <param name="t">Type to register (must not be null).</param>
        /// <returns>True if the type has been registered, false otherwise.</returns>
        public bool Register( IActivityMonitor monitor, Type t )
        {
            if( t == null ) throw new ArgumentNullException( nameof( t ) );
            return t.IsInterface && _actualPocoPredicate( monitor, t )
                    ? DoRegister( monitor, t ) != null
                    : false;
        }

        PocoType? DoRegister( IActivityMonitor monitor, Type t )
        {
            Debug.Assert( t.IsInterface && _actualPocoPredicate( monitor, t ) );
            if( !_all.TryGetValue( t, out var p ) )
            {
                p = CreatePocoType( monitor, t );
                _all.Add( t, p );
                if( p != null && p.Root == p ) _result.Add( p.RootCollector! );
            }
            return p;
        }

        PocoType? CreatePocoType( IActivityMonitor monitor, Type t )
        {
            if( !_typeFilter( monitor, t ) )
            {
                monitor.Info( $"Poco interface '{t.AssemblyQualifiedName}' is excluded." );
                return null;
            }
            PocoType? theOnlyRoot = null;
            foreach( Type b in t.GetInterfaces() )
            {
                if( b == typeof( IPoco ) || b == typeof( IClosedPoco ) ) continue;
                // Base interface must be a IPoco. This is a security to avoid "normal" interfaces to appear
                // by mistake in a Poco.
                if( !typeof( IPoco ).IsAssignableFrom( b ) )
                {
                    monitor.Fatal( $"Poco interface '{t.AssemblyQualifiedName}' extends '{b.Name}'. '{b.Name}' must be marked with CK.Core.IPoco interface." );
                    return null;
                }
                // Attempts to register the base if and only if it is not a "definer".
                if( _actualPocoPredicate( monitor, b ) )
                {
                    var baseType = DoRegister( monitor, b );
                    if( baseType == null ) return null;
                    // Detect multiple root Poco.
                    if( theOnlyRoot != null )
                    {
                        if( theOnlyRoot != baseType.Root )
                        {
                            monitor.Fatal( $"Poco interface '{t.AssemblyQualifiedName}' extends both '{theOnlyRoot.Type.Name}' and '{baseType.Root.Type.Name}' (via '{baseType.Type.Name}')." );
                            return null;
                        }
                    }
                    else theOnlyRoot = baseType.Root;
                }
            }
            return new PocoType( t, theOnlyRoot );
        }

        /// <summary>
        /// Finalize registrations by creating a <see cref="IPocoSupportResult"/> or null on error.
        /// The <see cref="EmptyPocoSupportResult.Default"/> singleton can be used wherever null
        /// references must be avoided.
        /// </summary>
        /// <param name="assembly">The dynamic assembly: its <see cref="IDynamicAssembly.StubModuleBuilder"/> will host the generated stub.</param>
        /// <param name="monitor">Monitor to use.</param>
        /// <returns>The result or null on error.</returns>
        public IPocoSupportResult? Finalize( IDynamicAssembly assembly, IActivityMonitor monitor )
        {
            return CreateResult( assembly, monitor );
        }

        Result? CreateResult( IDynamicAssembly assembly, IActivityMonitor monitor )
        {
            Result r = new Result();
            bool hasNameError = false;
            foreach( var signature in _result )
            {
                var cInfo = CreateClassInfo( assembly, monitor, signature );
                if( cInfo == null ) return null;
                r.Roots.Add( cInfo );

                foreach( var i in signature )
                {
                    Type iCreate = typeof( IPocoFactory<> ).MakeGenericType( i );
                    var iInfo = new InterfaceInfo( cInfo, i, iCreate );
                    cInfo.Interfaces.Add( iInfo );
                    r.AllInterfaces.Add( i, iInfo );
                }
                cInfo.OtherInterfaces.Remove( typeof( IPoco ) );
                cInfo.OtherInterfaces.Remove( typeof( IClosedPoco ) );
                foreach( var t in signature ) cInfo.OtherInterfaces.Remove( t );
                foreach( var e in cInfo.OtherInterfaces )
                {
                    IReadOnlyList<IPocoRootInfo>? value;
                    if( r.OtherInterfaces.TryGetValue( e, out value ) )
                    {
                        ((List<ClassInfo>)value).Add( cInfo );
                    }
                    else
                    {
                        r.OtherInterfaces.Add( e, new List<ClassInfo>() { cInfo } );
                    }
                }

                hasNameError |= !cInfo.InitializeNames( monitor );
            }
            return hasNameError
                   || !r.CheckPropertiesVarianceAndInstantiationCycleError( monitor )
                   || !r.BuildNameIndex( monitor )
                   ? null
                   : r;
        }

        static readonly MethodInfo _typeFromToken = typeof( Type ).GetMethod( nameof( Type.GetTypeFromHandle ), BindingFlags.Static | BindingFlags.Public )!;

        static ClassInfo? CreateClassInfo( IDynamicAssembly assembly, IActivityMonitor monitor, IReadOnlyList<Type> interfaces )
        {
            // The first interface is the PrimartyInterface: we use its name to drive the implementation name.
            string pocoTypeName = assembly.GetAutoImplementedTypeName( interfaces[0] );
            var moduleB = assembly.StubModuleBuilder;
            var tB = moduleB.DefineType( pocoTypeName );

            // The factory also ends with "_CK": it is a generated type.
            var tBF = moduleB.DefineType( pocoTypeName + "Factory_CK" );

            // The IPocoFactory base implementation.
            tBF.AddInterfaceImplementation( typeof( IPocoFactory ) );
            {
                MethodBuilder m = tBF.DefineMethod( "get_PocoDirectory", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( PocoDirectory ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.PocoDirectory ), PropertyAttributes.None, typeof( PocoDirectory ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "get_PocoClassType", MethodAttributes.Public | MethodAttributes.Virtual |  MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( Type ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldtoken, tB );
                g.Emit( OpCodes.Call, _typeFromToken );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.PocoClassType ), PropertyAttributes.None, typeof( Type ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "get_Name", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( string ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.Name ), PropertyAttributes.None, typeof( string ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "get_PreviousNames", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( IReadOnlyList<string> ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.PreviousNames ), PropertyAttributes.None, typeof( IReadOnlyList<string> ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "Create", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( IPoco ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
            }

            // The IPocoClass implementation.
            var properties = new Dictionary<string, PocoPropertyInfo>();
            var propertyList = new List<PocoPropertyInfo>();
            List<PropertyInfo>? externallyImplementedPropertyList = null;

            // This is required to handle "non actual Poco" (CKTypeDefiner "base type"): interfaces list
            // contains only actual IPoco, the expanded set contains the closure of all the interfaces.
            //
            // This work is the perfect opportunity to handle the "closed poco" feature without overhead:
            // by identifying the "biggest" interface in terms of base interfaces, we can check that it
            // actually closes the whole IPoco.
            //
            var expanded = new HashSet<Type>( interfaces );
            Type? closure = null;
            int maxICount = 0;
            bool mustBeClosed = false;
            foreach( var i in interfaces )
            {
                mustBeClosed |= typeof( IClosedPoco ).IsAssignableFrom( i ); 
                var bases = i.GetInterfaces();
                if( closure == null || maxICount < bases.Length )
                {
                    maxICount = bases.Length;
                    closure = i;
                }
                expanded.AddRange( bases );
                // Since we are iterating over the IPoco interfaces, we can build
                // the factory class that must support all the IPocoFactory<>.
                Type iCreate = typeof( IPocoFactory<> ).MakeGenericType( i );
                tBF.AddInterfaceImplementation( iCreate );
                {
                    MethodBuilder m = tBF.DefineMethod( "C" + expanded.Count.ToString( System.Globalization.NumberFormatInfo.InvariantInfo ), MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final, i, Type.EmptyTypes );
                    ILGenerator g = m.GetILGenerator();
                    g.Emit( OpCodes.Ldnull );
                    g.Emit( OpCodes.Ret );
                    tBF.DefineMethodOverride( m, iCreate.GetMethod( nameof( IPocoFactory<IPoco>.Create ) )! );
                }
            }
            // If the IClosedPoco has been found, we ensure that a closure interface has been found.
            if( mustBeClosed )
            {
                Debug.Assert( maxICount < expanded.Count );
                if( maxICount < expanded.Count - 1 )
                {
                    monitor.Error( $"Poco family '{interfaces.Select( b => b.FullName ).Concatenate("', '")}' must be closed but none of these interfaces covers the other ones." );
                    return null;
                }
                Debug.Assert( closure != null, "Since there is at least one interface." );
                monitor.Debug( $"{closure.FullName}: IClosedPoco for {interfaces.Select( b => b.FullName ).Concatenate()}." );
            }
            // Supports the IPocoClass interface.
            tB.AddInterfaceImplementation( typeof( IPocoClass ) );
            {
                MethodBuilder m = tB.DefineMethod( "get_Factory", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( IPocoFactory ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tB.DefineProperty( nameof( IPocoClass.Factory ), PropertyAttributes.None, typeof( IPocoFactory ), null );
                p.SetGetMethod( m );
            }

            // For each expanded interfaces (all of them: the Interfaces and the OtherInterfaces):
            // - Implements the interface on the PocoClass (tB).
            // - Registers the properties and creates the PocoPropertyInfo.
            bool hasPropertyError = false;
            foreach( var i in expanded )
            {
                tB.AddInterfaceImplementation( i );

                // Analyzing interface properties.
                // For union types, the UnionTypes nested struct fields are cached once.
                PropertyInfo[]? unionTypesDef = null; 

                foreach( var p in i.GetProperties() )
                {
                    // As soon as a property claims to be implemented, we remove it from the properties.
                    if( p.GetCustomAttributesData().Any( d => d.AttributeType.Name == nameof( AutoImplementationClaimAttribute ) ) )
                    {
                        if( externallyImplementedPropertyList == null ) externallyImplementedPropertyList = new List<PropertyInfo>();
                        externallyImplementedPropertyList.Add( p );
                        if( properties.TryGetValue( p.Name, out var implP ) )
                        {
                            propertyList.RemoveAt( implP.Index );
                            for( int idx = implP.Index; idx < propertyList.Count; ++idx )
                            {
                                --propertyList[idx].Index;
                            }
                        }
                    }
                    else
                    {
                        hasPropertyError &= HandlePocoProperty( monitor, expanded, properties, propertyList, ref unionTypesDef, i, p );
                    }
                }
            }
            if( hasPropertyError ) return null;

            // Implements the stubs for each externally implemented property.
            if( externallyImplementedPropertyList != null )
            {
                foreach( var p in externallyImplementedPropertyList )
                {
                    EmitHelper.ImplementStubProperty( tB, p, isVirtual: false, alwaysImplementSetter: false );
                }
            }

            // Handles default values and implements the stubs for each
            // PocoPropertyInfo.
            foreach( var p in propertyList )
            {
                if( !InitializeDefaultValue( p, monitor ) ) return null;

                if( p.IsReadOnly && p.DefaultValueSource != null )
                {
                    monitor.Error( $"Property '{p.PropertyType.DeclaringType!.FullName}.{p.PropertyName}' of type {p.PropertyType.Name} cannot have a default value attribute: [DefaultValue( {p.DefaultValueSource} )]." );
                    return null;
                }
                foreach( var propInfo in p.DeclaredProperties )
                {
                    EmitHelper.ImplementStubProperty( tB, propInfo, isVirtual: false );
                }
            }

            var tPoCo = tB.CreateType();
            Debug.Assert( tPoCo != null );

            var tPocoFactory = tBF.CreateType();
            Debug.Assert( tPocoFactory != null );

            return new ClassInfo( tPoCo, tPocoFactory, mustBeClosed, closure, expanded, properties, propertyList, externallyImplementedPropertyList );
        }

        static bool HandlePocoProperty( IActivityMonitor monitor,
                                        HashSet<Type> expanded,
                                        Dictionary<string, PocoPropertyInfo> properties,
                                        List<PocoPropertyInfo> propertyList,
                                        ref PropertyInfo[]? unionTypesDef,
                                        Type interfaceType,
                                        PropertyInfo p )
        {
            // We cannot check the equality of property type here because we need to consider IPoco families: we
            // have to wait that all of them have been registered.
            // ClassInfo.CheckPropertiesVarianceAndUnionTypes checks the type and the nullability.
            var isReadOnly = !p.CanWrite;
            if( properties.TryGetValue( p.Name, out var implP ) )
            {
                implP.DeclaredProperties.Add( p );
                if( implP.IsReadOnly != isReadOnly )
                {
                    Type iW = implP.DeclaredProperties[0].DeclaringType!;
                    Type iR = interfaceType;
                    if( isReadOnly ) (iR, iW) = (iW, iR);
                    monitor.Error( $"Interface '{iR}' and '{iW}' both declare property '{p.Name}' but the first is readonly and the latter is read/write. The same property must be readonly or read/write for all the interfaces that define it." );
                    return false;
                }
            }
            else
            {
                implP = new PocoPropertyInfo( p, propertyList.Count );
                properties.Add( p.Name, implP );
                propertyList.Add( implP );
                if( !p.CanRead )
                {
                    monitor.Error( $"Poco property '{interfaceType.FullName}.{p.Name}' cannot be read." );
                    return false;
                }
                var nullability = p.GetNullabilityInfo();
                if( nullability.Kind.IsNullable() && isReadOnly )
                {
                    monitor.Error( $"Poco property '{interfaceType.FullName}.{p.Name}' type is nullable and readonly (it has no setter). This is forbidden since value will always be null." );
                    return false;
                }
                if( isReadOnly )
                {
                    bool isAllowedType = false;
                    if( typeof( IPoco ).IsAssignableFrom( p.PropertyType ) )
                    {
                        if( expanded.Contains( p.PropertyType ) )
                        {
                            monitor.Error( $"Poco Cyclic dependency error: readonly property '{interfaceType.FullName}.{p.Name}' references its own Poco type." );
                            return false;
                        }
                        isAllowedType = true;
                        // Testing whether they are actual IPoco (ie. not excluded from Setup) and don't create
                        // instantiation cycles is deferred when the global result is built.
                    }
                    else if( p.PropertyType.IsGenericType )
                    {
                        Type genType = p.PropertyType.GetGenericTypeDefinition();
                        isAllowedType = genType == typeof( IList<> ) || genType == typeof( List<> )
                                        || genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> )
                                        || genType == typeof( ISet<> ) || genType == typeof( HashSet<> );
                    }
                    if( !isAllowedType )
                    {
                        monitor.Error( $"Invalid Poco readonly property '{interfaceType.FullName}.{p.Name}': read only property type can only be another IPoco or a IList<>, List<>, IDictionary<,>, Dictionary<,>, ISet<> or HashSet<>. (Property's type is '{p.PropertyType.Name}'.)" );
                        return false;
                    }
                }
                implP.IsReadOnly = isReadOnly;
                implP.PropertyNullabilityInfo = nullability;
            }
            // UnionType handling.
            // If the property is a union, it cannot be readonly (just like for null). 
            var uAttr = p.GetCustomAttributes<UnionTypeAttribute>().FirstOrDefault();
            if( uAttr != null )
            {
                if( implP.IsReadOnly )
                {
                    monitor.Error( $"Invalid readonly [UnionType] '{interfaceType.FullName}.{p.Name}' property: a readonly union is forbidden. Allowed readonly property types are non nullable IPoco or a IList<>, List<>, IDictionary<,>, Dictionary<,>, ISet<> or HashSet<>." );
                    return false;
                }
                bool isPropertyNullable = implP.PropertyNullabilityInfo.Kind.IsNullable();
                List<string>? typeDeviants = null;
                List<string>? nullDeviants = null;

                if( unionTypesDef == null )
                {
                    Type? u = interfaceType.GetNestedType( "UnionTypes", BindingFlags.Public | BindingFlags.NonPublic );
                    if( u == null )
                    {
                        monitor.Error( $"[UnionType] attribute on '{interfaceType.FullName}.{p.Name}' requires a nested 'class UnionTypes {{ public (int?,string) {p.Name} {{ get; }} }}' with the types (here, (int?,string) is just an example of course)." );
                        return false;
                    }
                    unionTypesDef = u.GetProperties();
                }
                var f = unionTypesDef.FirstOrDefault( f => f.Name == p.Name );
                if( f == null )
                {
                    monitor.Error( $"The nested class UnionTypes requires a public value tuple '{p.Name}' property." );
                    return false;
                }
                if( !typeof( ITuple ).IsAssignableFrom( f.PropertyType ) )
                {
                    monitor.Error( $"The '{p.Name}' property of the nested class UnionTypes must be a value tuple (current type is {f.PropertyType.Name})." );
                    return false;
                }
                var nullableTypeTrees = f.GetNullableTypeTree();
                bool atLeastOneVariantIsNullable = false;
                foreach( var sub in nullableTypeTrees.SubTypes )
                {
                    if( !p.PropertyType.IsAssignableFrom( sub.Type ) )
                    {
                        if( typeDeviants == null ) typeDeviants = new List<string>();
                        typeDeviants.Add( sub.ToString() );
                    }
                    // If the property is nullable, the variants can be nullable or not.
                    // If the property is not nullable then the variants MUST NOT be nullable.
                    if( sub.Kind.IsNullable() )
                    {
                        if( !isPropertyNullable )
                        {
                            if( nullDeviants == null ) nullDeviants = new List<string>();
                            nullDeviants.Add( sub.ToString() );
                        }
                        atLeastOneVariantIsNullable = true;
                    }
                }
                if( typeDeviants != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{interfaceType.FullName}.{p.Name}'. Union type{(typeDeviants.Count > 1 ? "s" : "")} '{typeDeviants.Concatenate( "' ,'" )}' {(typeDeviants.Count > 1 ? "are" : "is")} incompatible with the property type '{p.PropertyType.Name}'." );
                }
                if( nullDeviants != null )
                {
                    Debug.Assert( !isPropertyNullable );
                    monitor.Error( $"Invalid [UnionType] attribute on '{interfaceType.FullName}.{p.Name}'. Union type{(nullDeviants.Count > 1 ? "s" : "")} '{nullDeviants.Concatenate( "' ,'" )}' must NOT be nullable since '{p.PropertyType.Name} {p.Name} {{ get; }}' is not nullable." );
                    return false;
                }
                if( isPropertyNullable && !atLeastOneVariantIsNullable )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{interfaceType.FullName}.{p.Name}'. None of the union types are nullable but '{p.PropertyType.Name}? {p.Name} {{ get; }}' is nullable." );
                    return false;
                }
                if( typeDeviants != null ) return false;

                var types = nullableTypeTrees.SubTypes.ToList();
                if( types.Any( t => t.Type == typeof( object ) ) ) 
                {
                    monitor.Error( $"{implP}': UnionTypes cannot define the type 'object' since this would erase all possible types." );
                    return false;
                }
                if( !implP.AddUnionPropertyTypes( monitor, nullableTypeTrees.SubTypes.ToList(), uAttr.CanBeExtended ) )
                {
                    monitor.Error( $"{implP}': [UnionType( CanBeExtended = true )] should be used on all interfaces or all unioned types must be the same." );
                    return false;
                }
            }
            return true;
        }

        static bool InitializeDefaultValue( PocoPropertyInfo p, IActivityMonitor monitor )
        {
            bool success = true;
            var aDefs = p.DeclaredProperties.Select( x => (Prop: x, x.GetCustomAttribute<DefaultValueAttribute>()) )
                                            .Where( a => a.Item2 != null )
                                            .Select( a => (a.Prop, a.Item2!.Value) );

            var first = aDefs.FirstOrDefault();
            if( first.Prop != null )
            {
                p.HasDefaultValue = true;
                p.DefaultValue = first.Value;
                var w = new StringCodeWriter();
                string defaultSource = p.DefaultValueSource = w.Append( first.Value ).ToString();
                foreach( var other in aDefs.Skip( 1 ) )
                {
                    w.StringBuilder.Clear();
                    var o = w.Append( other.Value ).ToString();
                    if( defaultSource != o )
                    {
                        monitor.Error( $"Default values difference between {first.Prop.DeclaringType}.{first.Prop.Name} = {defaultSource} and {other.Prop.DeclaringType}.{other.Prop.Name} = {o}." );
                        success = false;
                    }
                }
            }
            return success;
        }


    }
}

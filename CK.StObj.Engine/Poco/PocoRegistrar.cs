using CK.CodeGen;
using CK.Core;
using CK.Reflection;
using CK.Setup;
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
    /// Registrar for <see cref="IPoco"/> interfaces.
    /// </summary>
    partial class PocoRegistrar
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
        /// Initializes a new <see cref="PocoRegistrar"/>.
        /// </summary>
        /// <param name="actualPocoPredicate">
        /// This must be true for actual IPoco interfaces: when false, "base interface" are not directly registered.
        /// This implements the <see cref="CKTypeDefinerAttribute"/> behavior.
        /// </param>
        /// <param name="namespace">Namespace into which dynamic types will be created.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        public PocoRegistrar( Func<IActivityMonitor, Type, bool> actualPocoPredicate, string @namespace = "CK.GPoco", Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            Throw.CheckNotNullArgument( actualPocoPredicate );
            Throw.CheckNotNullArgument( @namespace );
            _actualPocoPredicate = actualPocoPredicate;
            _namespace = @namespace;
            _all = new Dictionary<Type, PocoType?>();
            _result = new List<List<Type>>();
            _typeFilter = typeFilter ?? (( m, type ) => true);
        }

        /// <summary>
        /// Registers a type that must be an interface that may be a <see cref="IPoco"/> interface.
        /// </summary>
        /// <param name="monitor">Monitor that will be used to signal errors.</param>
        /// <param name="t">Interface type to register (must not be null).</param>
        /// <returns>True if the type has been registered, false otherwise.</returns>
        public bool RegisterInterface( IActivityMonitor monitor, Type t )
        {
            Throw.CheckArgument( t?.IsInterface is true );
            return _actualPocoPredicate( monitor, t )
                    ? DoRegisterInterface( monitor, t ) != null
                    : false;
        }

        //public bool RegisterPocoClass( IActivityMonitor monitor, Type t )
        //{
        //    Throw.CheckArgument( t?.IsClass is true );
        //    return true;
        //}

        PocoType? DoRegisterInterface( IActivityMonitor monitor, Type t )
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
                //// Base interface must be a IPoco. This is a security to avoid "normal" interfaces to appear
                //// by mistake in a Poco.
                //// To allow this, base interface MUST be marked with ExcludeCKTypeAttribute (duck typing).
                //if( !typeof( IPoco ).IsAssignableFrom( b )
                //    && !b.GetCustomAttributesData().Any( a => a.AttributeType.Name == nameof(ExcludeCKTypeAttribute) ) )
                //{
                //    monitor.Fatal( $"Poco interface '{t.AssemblyQualifiedName}' extends '{b.Name}'. '{b.Name}' must also be a CK.Core.IPoco interface or a [ExcludeCKType] attribute." );
                //    return null;
                //}
                // Attempts to register the base if and only if it is not a "definer".
                if( _actualPocoPredicate( monitor, b ) )
                {
                    var baseType = DoRegisterInterface( monitor, b );
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
                        ((List<PocoRootInfo>)value).Add( cInfo );
                    }
                    else
                    {
                        r.OtherInterfaces.Add( e, new List<PocoRootInfo>() { cInfo } );
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

        static PocoRootInfo? CreateClassInfo( IDynamicAssembly assembly, IActivityMonitor monitor, IReadOnlyList<Type> interfaces )
        {
            // The first interface is the PrimartyInterface: we use its name to drive the implementation name.
            var primary = interfaces[0];
            if( primary.IsGenericType )
            {
                monitor.Error( $"The IPoco interface '{primary}' cannot be a generic type (different extensions could use different types for the same type parameter). Use the [CKTypeDefiner] attribute to define a generic IPoco." );
                return null;
            }
            string pocoTypeName = assembly.GetAutoImplementedTypeName( primary );
            var moduleB = assembly.StubModuleBuilder;
            var tB = moduleB.DefineType( pocoTypeName, TypeAttributes.Sealed );

            // The factory also ends with "_CK": it is a generated type.
            var tBF = moduleB.DefineType( pocoTypeName + "Factory_CK", TypeAttributes.Sealed );

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
                MethodBuilder m = tBF.DefineMethod( "get_Interfaces", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( IReadOnlyList<Type> ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.Interfaces ), PropertyAttributes.None, typeof( IReadOnlyList<Type> ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "get_PrimaryInterface", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( Type ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.PrimaryInterface ), PropertyAttributes.None, typeof( Type ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "get_ClosureInterface", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( Type ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.ClosureInterface ), PropertyAttributes.None, typeof( Type ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "get_IsClosedPoco", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( bool ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldc_I4_0 );
                g.Emit( OpCodes.Ret );
                var p = tBF.DefineProperty( nameof( IPocoFactory.IsClosedPoco ), PropertyAttributes.None, typeof( bool ), null );
                p.SetGetMethod( m );
            }
            {
                MethodBuilder m = tBF.DefineMethod( "Create", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( IPoco ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
            }

            // The IPocoGeneratedClass implementation.
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
            Debug.Assert( closure != null, "Since there is at least one interface." );

            // Is the biggest interface the closure?
            if( maxICount < expanded.Count - 1 )
            {
                closure = null;
            }
            // If the IClosedPoco has been found, we ensure that a closure interface has been found.
            if( mustBeClosed )
            {
                Debug.Assert( maxICount < expanded.Count );
                if( closure == null )
                {
                    monitor.Error( $"Poco family '{interfaces.Select( b => b.ToCSharpName() ).Concatenate("', '")}' must be closed but none of these interfaces covers the other ones." );
                    return null;
                }
                monitor.Trace( $"{closure.FullName}: IClosedPoco for {interfaces.Select( b => b.ToCSharpName() ).Concatenate()}." );
            }
            // Supports the IPocoGeneratedClass interface.
            tB.AddInterfaceImplementation( typeof( IPocoGeneratedClass ) );
            {
                MethodBuilder m = tB.DefineMethod( "get_Factory", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( IPocoFactory ), Type.EmptyTypes );
                ILGenerator g = m.GetILGenerator();
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Ret );
                var p = tB.DefineProperty( nameof( IPocoGeneratedClass.Factory ), PropertyAttributes.None, typeof( IPocoFactory ), null );
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
                    monitor.Error($"Read only {p} cannot have a default value attribute: [DefaultValue( {p.DefaultValueSource} )].");
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

            return new PocoRootInfo( tPoCo, tPocoFactory, mustBeClosed, closure, expanded, properties, propertyList, externallyImplementedPropertyList );
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
            // Same as the Poco-like: it's easier to consider them once IPoco analysis is done.
            // ClassInfo.CheckPropertiesVarianceAndUnionTypes checks the type and the nullability.
            bool success = true;
            var isReadOnly = !p.CanWrite;
            if( properties.TryGetValue( p.Name, out var implP ) )
            {
                // Already defined on a previously analyzed interface.
                implP.DeclaredProperties.Add( p );
                if( implP.IsReadOnly != isReadOnly )
                {
                    Type iW = implP.DeclaredProperties[0].DeclaringType!;
                    Type iR = interfaceType;
                    if( isReadOnly ) (iR, iW) = (iW, iR);
                    monitor.Error( $"Interface '{iR.ToCSharpName()}' and '{iW.ToCSharpName()}' both declare property '{p.Name}' but the first is readonly and the latter is read/write. The same property must be readonly or read/write for all the interfaces that define it." );
                    success = false;
                }
            }
            else
            {
                // New property.

                // We must always create it, even if an error is detected to let the dynamic generation
                // of the runtime Poco class (with fake getters/setters) succeed.
                if( !p.CanRead )
                {
                    monitor.Error( $"Poco property '{interfaceType.ToCSharpName()}.{p.Name}' cannot be read." );
                    success = false;
                }
                var nullabilityInfo = p.GetNullabilityInfo();
                var nullTree = p.PropertyType.GetNullableTypeTree( nullabilityInfo, NullableTypeTree.ObliviousDefaultBuilder );
                bool isBasicProperty = PocoSupportResultExtension.IsBasicPropertyType( nullTree.Type );
                Type? genRefType = nullTree.Kind.IsReferenceType() && nullTree.Type.IsGenericType ? nullTree.Type.GetGenericTypeDefinition() : null;
                bool isReadonlyCompliantCollection = genRefType != null && (genRefType == typeof( List<> )
                                                                            || genRefType == typeof( Dictionary<,> )
                                                                            || genRefType == typeof( HashSet<> ));
                bool isStandardCollection = isReadonlyCompliantCollection || p.PropertyType.IsArray;
                if( isReadOnly && success )
                {
                    // Basic checks that don't require all IPoco to be discovered (kind of fail fast).
                    if( nullTree.Kind.IsNullable() )
                    {
                        monitor.Error( $"Poco property '{interfaceType.ToCSharpName()}.{p.Name}' type is nullable and readonly (it has no setter). This is forbidden since value will always be null." );
                        success = false;
                    }
                    else if( isBasicProperty )
                    {
                        monitor.Error( $"Poco property '{interfaceType.ToCSharpName()}.{p.Name}' type is a readonly basic type (it has no setter). This is forbidden since totally useless." );
                        success = false;
                    }
                    else if( isStandardCollection && !isReadonlyCompliantCollection )
                    {
                        monitor.Error( $"Poco property '{interfaceType.ToCSharpName()}.{p.Name}' type is a readonly array (it has no setter). This is forbidden since we could only generate an empty array." );
                        success = false;
                    }
                    else if( typeof( IPoco ).IsAssignableFrom( p.PropertyType ) && expanded.Contains( p.PropertyType ) )
                    {
                        monitor.Error( $"Poco Cyclic dependency error: readonly property '{interfaceType.FullName}.{p.Name}' references its own Poco type." );
                        success = false;
                        // Testing whether they are actual IPoco (ie. not excluded from Setup) and don't create
                        // instantiation cycles is deferred when the global result is built.
                    }
                }
                implP = new PocoPropertyInfo( p, propertyList.Count, isReadOnly, nullabilityInfo, nullTree, isStandardCollection );
                properties.Add( p.Name, implP );
                propertyList.Add( implP );
                implP.IsBasicPropertyType = isBasicProperty;
            }
            return success && HandleUnionTypesIfAny( monitor, ref unionTypesDef, interfaceType, p, implP );
        }

        static bool HandleUnionTypesIfAny( IActivityMonitor monitor, ref PropertyInfo[]? unionTypesDef, Type interfaceType, PropertyInfo p, PocoPropertyInfo implP )
        {
            var uAttr = p.GetCustomAttributes<UnionTypeAttribute>().FirstOrDefault();
            if( uAttr != null )
            {
                // A union type, it cannot be readonly. 
                if( implP.IsReadOnly )
                {
                    monitor.Error( $"Invalid readonly [UnionType] '{interfaceType.FullName}.{p.Name}' property: a readonly union is forbidden. Allowed readonly property types are non nullable IPoco or List<>, Dictionary<,> or Set<>." );
                    return false;
                }
                // A union type is not a basic property (fix the fact that typeof(object) is a basic property).
                implP.IsBasicPropertyType = false;
                bool isPropertyNullable = implP.PropertyNullableTypeTree.Kind.IsNullable();
                List<string>? typeDeviants = null;
                List<string>? nullableDef = null;
                List<string>? interfaceCollections = null;

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
                var tree = f.GetNullableTypeTree();
                if( (tree.Kind & NullabilityTypeKind.IsTupleType) == 0 )
                {
                    monitor.Error( $"The '{p.Name}' property of the nested class UnionTypes must be a value tuple (current type is {tree})." );
                    return false;
                }
                if( tree.Kind.IsNullable() != isPropertyNullable )
                {
                    monitor.Error( $"The '{p.Name}' property of the nested class UnionTypes must{(isPropertyNullable ? "" : "NOT")} BE nullable since the property itself is nullable." );
                    return false;
                }
                foreach( var sub in tree.SubTypes )
                {
                    if( !p.PropertyType.IsAssignableFrom( sub.Type ) )
                    {
                        if( typeDeviants == null ) typeDeviants = new List<string>();
                        typeDeviants.Add( sub.ToString() );
                    }
                    else if( sub.Type.IsGenericType )
                    {
                        var tGen = sub.Type.GetGenericTypeDefinition();
                        if( tGen == typeof( IList<> ) )
                        {
                            if( interfaceCollections == null ) interfaceCollections = new List<string>();
                            interfaceCollections.Add( $"{sub} should be a List<{sub.RawSubTypes[0]}>" );
                        }
                        else if( tGen == typeof( IDictionary<,> ) )
                        {
                            if( interfaceCollections == null ) interfaceCollections = new List<string>();
                            interfaceCollections.Add( $"{sub} should be a Dictionary<{sub.RawSubTypes[0]},{sub.RawSubTypes[1]}>" );
                        }
                        else if( tGen == typeof( ISet<> ) )
                        {
                            if( interfaceCollections == null ) interfaceCollections = new List<string>();
                            interfaceCollections.Add( $"{sub} should be a HashSet<{sub.RawSubTypes[0]}>" );
                        }
                    }
                    if( sub.Kind.IsNullable() )
                    {
                        if( nullableDef == null ) nullableDef = new List<string>();
                        nullableDef.Add( sub.ToString() );
                    }
                }
                if( typeDeviants != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{interfaceType.FullName}.{p.Name}'. Union type{(typeDeviants.Count > 1 ? "s" : "")} '{typeDeviants.Concatenate( "' ,'" )}' {(typeDeviants.Count > 1 ? "are" : "is")} incompatible with the property type '{p.PropertyType.Name}'." );
                }
                if( interfaceCollections != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{interfaceType.FullName}.{p.Name}'. Collection types must be concrete: {interfaceCollections.Concatenate()}." );
                }
                if( nullableDef != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{interfaceType.FullName}.{p.Name}'. Union type definitions must not be nullable: please change '{nullableDef.Concatenate( "' ,'" )}' to be not nullable." );
                    return false;
                }
                if( typeDeviants != null || interfaceCollections != null ) return false;

                // Type definitions are non-nullable.
                var types = tree.SubTypes.ToList();
                if( types.Any( t => t.Type == typeof( object ) ) )
                {
                    monitor.Error( $"{implP}': UnionTypes cannot define the type 'object' since this would erase all possible types." );
                    return false;
                }
                if( !implP.AddUnionPropertyTypes( monitor, types, uAttr.CanBeExtended ) )
                {
                    monitor.Error( $"{implP}': [UnionType( CanBeExtended = true )] should be used on all interfaces or all unioned type definitions across IPoco family must be the same." );
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

using CK.Core;
using CK.Setup;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CK.Setup;

/// <summary>
/// Registrar for <see cref="IPoco"/> interfaces.
/// This is a mess: CKTypeKindDetector can handle Abstract/Secondary/Primary
/// classification directly.
/// What should be kept here is the emit of the stubs, but the structure should be done
/// by CKTypeKindDetector and PocoTypeSystemBuilder.
/// </summary>
partial class PocoDirectoryBuilder
{
    sealed class InterfaceEntry
    {
        public readonly Type Type;
        public readonly InterfaceEntry Primary;
        public readonly List<Type>? RootCollector;

        public InterfaceEntry( Type type, InterfaceEntry? primary )
        {
            Type = type;
            if( primary != null )
            {
                Debug.Assert( primary.RootCollector != null, "The root centralizes the collector." );
                Primary = primary;
                primary.RootCollector.Add( type );
            }
            else
            {
                Primary = this;
                RootCollector = new List<Type>() { type };
            }
        }
    }

    readonly Dictionary<Type, InterfaceEntry?> _all;
    readonly List<List<Type>> _result;
    readonly string _namespace;
    readonly IExtMemberInfoFactory _memberInfoFactory;
    readonly CKTypeKindDetector _kindDetector;
    // Yet another patch to save the Abstracts that are only discovered by the
    // legacy code IF they have a Primary. This used to be a feature... but
    // now we have the implementation less handling in the PocoTypeSystemBuilder.
    readonly HashSet<Type> _definers;

    internal PocoDirectoryBuilder( IExtMemberInfoFactory memberInfoFactory,
                                   CKTypeKindDetector kindDetector,
                                   string @namespace = "CK.GPoco" )
    {
        _memberInfoFactory = memberInfoFactory;
        _kindDetector = kindDetector;
        _namespace = @namespace;
        _all = new Dictionary<Type, InterfaceEntry?>();
        _result = new List<List<Type>>();
        _definers = new HashSet<Type>();
    }

    bool IsPoco( IActivityMonitor monitor, Type t ) => (_kindDetector.GetRawKind( monitor, t ) & (CKTypeKind.IsPoco | CKTypeKind.IsExcludedType)) == CKTypeKind.IsPoco;

    bool IsPocoButNotDefiner( IActivityMonitor monitor, Type t ) => (_kindDetector.GetNonDefinerKind( monitor, t ) & (CKTypeKind.IsPoco | CKTypeKind.IsExcludedType)) == CKTypeKind.IsPoco;

    public bool RegisterInterface( IActivityMonitor monitor, Type t, CKTypeKind rawKind ) => DoRegisterInterface( monitor, t, rawKind ) != null;

    InterfaceEntry? DoRegisterInterface( IActivityMonitor monitor, Type t, CKTypeKind rawKind )
    {
        Throw.DebugAssert( t.IsInterface && t.IsVisible && !t.IsGenericTypeDefinition && (rawKind & (CKTypeKind.IsPoco | CKTypeKind.IsExcludedType)) == CKTypeKind.IsPoco );
        if( (rawKind & CKTypeKind.IsDefiner) != 0 )
        {
            // Keep IPoco itself exclusion exception here.
            if( t != typeof( IPoco ) ) _definers.Add( t );
            return null;
        }
        if( !_all.TryGetValue( t, out var p ) )
        {
            p = TryCreateInterfaceEntry( monitor, t );
            _all.Add( t, p );
            if( p != null && p.Primary == p ) _result.Add( p.RootCollector! );
        }
        return p;
    }

    InterfaceEntry? TryCreateInterfaceEntry( IActivityMonitor monitor, Type t )
    {
        InterfaceEntry? singlePrimary = null;
        Type[] bs = t.GetInterfaces();
        if( bs.Length == 0 ) return null;
        foreach( Type b in bs )
        {
            if( b == typeof( IPoco ) || b.IsGenericTypeDefinition ) continue;
            // Attempts to register the base if and only if it is not a definer and it is a public interface.
            if( !b.IsVisible ) continue;
            var rawKind = _kindDetector.GetRawKind( monitor, b );
            if( (rawKind & (CKTypeKind.IsPoco | CKTypeKind.IsExcludedType)) == CKTypeKind.IsPoco )
            {
                var baseType = DoRegisterInterface( monitor, b, rawKind );
                // Excluded Poco interfaces are null here. Let's continue.
                // Errors are detected by a collector on the monitor anyway.
                if( baseType == null ) continue;
                // Detect multiple root Poco.
                if( singlePrimary != null )
                {
                    if( singlePrimary != baseType.Primary )
                    {
                        monitor.Fatal( $"Poco interface '{t:N}' extends both '{singlePrimary.Type.Name}' and '{baseType.Primary.Type.Name}' (via '{baseType.Type.Name}')." );
                        return null;
                    }
                }
                else singlePrimary = baseType.Primary;
            }
        }
        return new InterfaceEntry( t, singlePrimary );
    }

    /// <summary>
    /// Finalize registrations by creating a <see cref="IPocoDirectory"/> or null on error.
    /// The <see cref="EmptyPocoDirectory.Default"/> singleton can be used wherever null
    /// references must be avoided.
    /// </summary>
    /// <param name="assembly">The dynamic assembly: its <see cref="IDynamicAssembly.StubModuleBuilder"/> will host the generated stub.</param>
    /// <param name="monitor">Monitor to use.</param>
    /// <param name="regularTypeCollector">global type collector... This should be refactored.</param>
    /// <returns>The result or null on error.</returns>
    public IPocoDirectory? Build( IDynamicAssembly assembly, IActivityMonitor monitor, IReadOnlyDictionary<Type, TypeAttributesCache?> regularTypeCollector )
    {
        Result r = new Result();
        bool hasNameError = false;
        foreach( var signature in _result )
        {
            var cInfo = CreateClassInfo( assembly, _memberInfoFactory, monitor, signature, regularTypeCollector );
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
            foreach( var t in signature ) cInfo.OtherInterfaces.Remove( t );
            foreach( var e in cInfo.OtherInterfaces )
            {
                if( r.OtherInterfaces.TryGetValue( e, out var value ) )
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
        // Auwful fill...
        // Now the registered abstracts without implementations appear
        // in the OtherInterfaces with... no implementation.
        foreach( var def in _definers )
        {
            if( !r.OtherInterfaces.ContainsKey( def ) )
            {
                r.OtherInterfaces.Add( def, Array.Empty<PocoRootInfo>() );
            }
        }
        return hasNameError || !r.BuildNameIndex( monitor ) ? null : r;
    }

    static readonly MethodInfo _typeFromToken = typeof( Type ).GetMethod( nameof( Type.GetTypeFromHandle ), BindingFlags.Static | BindingFlags.Public )!;

    static PocoRootInfo? CreateClassInfo( IDynamicAssembly assembly,
                                          IExtMemberInfoFactory memberInfoFactory,
                                          IActivityMonitor monitor,
                                          IReadOnlyList<Type> interfaces,
                                          IReadOnlyDictionary<Type, TypeAttributesCache?> regularTypeCollector )
    {
        // The first interface is the PrimartyInterface: we use its name to drive the implementation name.
        var primary = interfaces[0];
        if( primary.IsGenericType )
        {
            monitor.Error( $"The IPoco interface '{primary:N}' cannot be a generic type (different extensions could use different types for the same type parameter). Use the [CKTypeDefiner] attribute to define a generic IPoco." );
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
            MethodBuilder m = tBF.DefineMethod( "get_PocoClassType", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof( Type ), Type.EmptyTypes );
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
        List<string>? dimPropertyNames = null;

        // This is required to handle "abstract" IPoco (CKTypeDefiner "base type"): interfaces list
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
                monitor.Error( $"Poco family '{interfaces.Select( b => b.ToCSharpName() ).Concatenate( "', '" )}' must be closed but none of these interfaces covers the other ones." );
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
        bool success = true;
        int uniquePropertyIndex = 0;
        foreach( var i in expanded )
        {
            tB.AddInterfaceImplementation( i );

            // Caches the "class UnionType" properties.
            MemberInfo[]? cacheUnionTypesDef = null;
            var propertyInfos = i.GetProperties();
            foreach( var p in propertyInfos )
            {
                // Handle stupidity early.
                if( !p.CanRead )
                {
                    monitor.Error( $"Poco property '{i}.{p.Name}' cannot be read. This is forbidden." );
                    success = false;
                }
                else
                {
                    Debug.Assert( p.GetMethod != null );
                    // As soon as a property claims to be implemented, we remove it from the PocoProperties.
                    //
                    // C#8 introduced Default Implementation Methods (DIM). It MUST be an "AutoImplementationClaim":
                    // they don't appear as Poco properties. This perfectly fits the DIM design: they can be called only
                    // through the interface, not from the implementing class nor from other interfaces.
                    //
                    // One could be tempted here to support some automatic (intelligent?) support for this like for
                    // instance a [SharedImplementation] attributes that will make the DIM visible (and relayed to the DIM
                    // implementation) from the other Poco interfaces. This is not really difficult and de facto implement
                    // a multiple inheritance capability... However, I'm a bit reluctant to do this since this would transform
                    // IPoco from a DTO structure to an Object beast. Such IPoco become far less "exchangeable" with the external
                    // world since they would lost their behavior. The funny Paradox here is that this would not be a real issue
                    // with "real" Methods that do things: nobody will be surprised to have "lost" these methods in Type Script
                    // for instance, but for DIM properties (typically computed values) this will definitely be surprising.
                    // In practice, the code would often has to be transfered "on the other side", with the data...
                    //
                    // Choosing here to NOT play the multiple inheritance game is clearly the best choice (at least for me :)).
                    //
                    IExtPropertyInfo extP = memberInfoFactory.Create( p );
                    if( extP.CustomAttributesData.Any( d => d.AttributeType.Name == nameof( AutoImplementationClaimAttribute ) ) )
                    {
                        bool isDIM = (p.GetMethod.Attributes & MethodAttributes.Abstract) == 0;
                        if( isDIM )
                        {
                            monitor.Info( $"Property '{i}.{p.Name}' is a valid Default Implemented Method (DIM) with a [AutoImplementationClaim] attribute." );
                            dimPropertyNames ??= new List<string>();
                            dimPropertyNames.Add( p.Name );

                        }
                        else
                        {
                            monitor.Info( $"Property '{i}.{p.Name}' has a [AutoImplementationClaim] attribute. The property implementation won't be automatically generated." );
                        }
                        externallyImplementedPropertyList ??= new List<PropertyInfo>();
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
                        // Quick check of UnionType attribute existence.
                        bool hasUnionType = extP.CustomAttributesData.Any( a => a.AttributeType == typeof( UnionTypeAttribute ) );
                        if( hasUnionType && cacheUnionTypesDef == null )
                        {
                            Type? u = i.GetNestedType( "UnionTypes", BindingFlags.Public | BindingFlags.NonPublic );
                            if( u == null )
                            {
                                monitor.Error( $"[UnionType] attribute on '{i.ToCSharpName()}.{p.Name}' requires a nested " +
                                               $"'class UnionTypes {{ public (int,string) {p.Name} {{ get; }} }}' with the types. " +
                                               $"Here, (int,string) is just an example of course." );
                                success = false;
                                cacheUnionTypesDef = Array.Empty<PropertyInfo>();
                            }
                            else cacheUnionTypesDef = u.GetProperties();
                        }
                        success &= HandlePocoProperty( monitor,
                                                       memberInfoFactory,
                                                       properties,
                                                       propertyList,
                                                       ref dimPropertyNames,
                                                       i,
                                                       extP,
                                                       hasUnionType ? cacheUnionTypesDef : null );
                    }
                    if( success )
                    {
                        // Always implement the stub as long as there is no error.
                        ImplementInterfaceProperty( tB, p, uniquePropertyIndex++ );
                    }
                }
            }
            // Creates a stub method for all non DIM methods.
            foreach( var method in i.GetMethods() )
            {
                // Filters out methods with special name (this skips get_XXX and set_XXX)
                // We keep static methods (even if we don't currently use them).
                if( (method.Attributes & MethodAttributes.SpecialName) != 0 ) continue;
                // Is this a Default Implementation Method?
                // If yes, we let it as is. 
                bool isDIM = (method.Attributes & MethodAttributes.Abstract) == 0;
                if( !isDIM )
                {
                    MethodAttributes mA = method.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.VtableLayoutMask);
                    var mB = tB.DefineMethod( method.Name, mA, method.ReturnType, method.GetParameters().Select( p => p.ParameterType ).ToArray() );
                    ILGenerator g = mB.GetILGenerator();
                    if( method.ReturnType != typeof( void ) )
                    {
                        var local = g.DeclareLocal( method.ReturnType );
                        g.Emit( OpCodes.Ldfld, local );
                        g.Emit( OpCodes.Initobj, method.ReturnType );
                        g.Emit( OpCodes.Ldfld, local );
                    }
                    g.Emit( OpCodes.Ret );
                }
            }
        }
        if( !success ) return null;

        var tPoCo = tB.CreateType();
        Debug.Assert( tPoCo != null );

        var tPocoFactory = tBF.CreateType();
        Debug.Assert( tPocoFactory != null );

        return new PocoRootInfo( tPoCo,
                                 tPocoFactory,
                                 mustBeClosed,
                                 closure,
                                 expanded,
                                 properties,
                                 propertyList,
                                 externallyImplementedPropertyList );
    }

    static bool HandlePocoProperty( IActivityMonitor monitor,
                                    IExtMemberInfoFactory memberInfoFactory,
                                    Dictionary<string, PocoPropertyInfo> properties,
                                    List<PocoPropertyInfo> propertyList,
                                    ref List<string>? dimPropertyNames,
                                    Type tInterface,
                                    IExtPropertyInfo p,
                                    MemberInfo[]? unionTypesDef )
    {
        Debug.Assert( p.DeclaringType == tInterface && p.PropertyInfo.GetMethod != null );

        if( (p.PropertyInfo.GetMethod.Attributes & MethodAttributes.Abstract) == 0 )
        {
            monitor.Error( $"Property '{tInterface}.{p.Name}' is a Default Implemented Method (DIM), it must use the [AutoImplementationClaim] attribute." );
            dimPropertyNames ??= new List<string>();
            dimPropertyNames.Add( p.Name );
            if( properties.TryGetValue( p.Name, out var implP ) )
            {
                monitor.Error( $"{implP}: all '{implP.Name}' properties must all be DIM and use the [AutoImplementationClaim] attribute." );
                return false;
            }
        }

        if( dimPropertyNames != null && dimPropertyNames.Any( dim => dim == p.Name ) )
        {
            monitor.Error( $"Property {tInterface}.{p.Name} has a Default Implementation Method (DIM). To be supported, all '{p.Name}' properties must be DIM and use the [AutoImplementationClaim] attribute." );
            return false;
        }

        // Creates the PocoPropertyInfo if this is the first PocoPropertyImpl
        if( !properties.TryGetValue( p.Name, out var pocoProperty ) )
        {
            // New property.
            pocoProperty = new PocoPropertyInfo( propertyList.Count, p.Name );
            properties.Add( p.Name, pocoProperty );
            propertyList.Add( pocoProperty );
        }
        // We'll need all nullability info and don't allow heterogeneous ones for poco
        // properties. Checks it once for all.
        if( p.GetHomogeneousNullabilityInfo( monitor ) == null )
        {
            return false;
        }
        pocoProperty.DeclaredProperties.Add( p );
        // Handles UnionType definition.
        if( unionTypesDef != null )
        {
            if( p.Type != typeof( object ) )
            {
                monitor.Error( $"{pocoProperty} is a UnionType: its type can only be 'object' or 'object?'." );
                return false;
            }
            var propDef = unionTypesDef.FirstOrDefault( f => f.Name == p.Name );
            if( propDef == null )
            {
                monitor.Error( $"The nested class UnionTypes requires a public value tuple '{p.Name}' property." );
                return false;
            }
            // The lookup in custom attributes data guaranties that the attribute exists.
            var attr = p.GetCustomAttributes<UnionTypeAttribute>().First();
            if( pocoProperty.UnionTypeDefinition == null )
            {
                pocoProperty.UnionTypeDefinition = new UnionTypeCollector( attr.CanBeExtended, memberInfoFactory.Create( propDef ) );
            }
            else
            {
                bool canBeExtended = pocoProperty.UnionTypeDefinition.CanBeExtended;
                if( canBeExtended != attr.CanBeExtended )
                {
                    monitor.Error( $"{pocoProperty} is a UnionType that can{(canBeExtended ? "" : "not")} be extended but '{p.DeclaringType.ToCSharpName( false )}.{p.Name}' can{(canBeExtended ? "not" : "")} be extended. All property definitions of a IPoco family must agree on this." );
                    return false;
                }
                if( !canBeExtended )
                {
                    monitor.Error( $"{pocoProperty} is a UnionType that cannot be extended." );
                    return false;
                }
                pocoProperty.UnionTypeDefinition.Types.Add( memberInfoFactory.Create( propDef ) );
            }
        }
        return true;
    }

    static PropertyBuilder ImplementInterfaceProperty( TypeBuilder tB, PropertyInfo property, int uid )
    {
        var pType = property.PropertyType.IsByRef
                        ? property.PropertyType.GetElementType()!
                        : property.PropertyType;
        FieldBuilder backField = tB.DefineField( "_" + property.Name + uid, pType, FieldAttributes.Private );

        MethodInfo? getMethod = property.GetMethod;
        MethodBuilder? mGet = null;
        if( getMethod != null )
        {
            MethodAttributes mA = getMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.VtableLayoutMask);
            mGet = tB.DefineMethod( getMethod.Name, mA, property.PropertyType, Type.EmptyTypes );
            ILGenerator g = mGet.GetILGenerator();
            g.LdArg( 0 );
            g.Emit( OpCodes.Ldfld, backField );
            g.Emit( OpCodes.Ret );
        }
        MethodInfo? setMethod = property.SetMethod;
        MethodBuilder? mSet = null;
        if( setMethod != null )
        {
            MethodAttributes mA = setMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.VtableLayoutMask);
            mSet = tB.DefineMethod( setMethod.Name, mA, typeof( void ), new[] { property.PropertyType } );
            ILGenerator g = mSet.GetILGenerator();
            g.LdArg( 0 );
            g.LdArg( 1 );
            g.Emit( OpCodes.Stfld, backField );
            g.Emit( OpCodes.Ret );
        }

        PropertyBuilder p = tB.DefineProperty( property.Name, property.Attributes, property.PropertyType, Type.EmptyTypes );
        if( mGet != null ) p.SetGetMethod( mGet );
        if( mSet != null ) p.SetSetMethod( mSet );
        return p;
    }
}

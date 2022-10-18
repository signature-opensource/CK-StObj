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
    partial class PocoDirectoryBuilder
    {
        sealed class InterfaceEntry
        {
            public readonly Type Type;
            public readonly InterfaceEntry Root;
            public readonly List<Type>? RootCollector;

            public InterfaceEntry( Type type, InterfaceEntry? root )
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

        readonly Dictionary<Type, InterfaceEntry?> _all;
        readonly List<List<Type>> _result;
        readonly string _namespace;
        readonly Func<IActivityMonitor, Type, bool> _typeFilter;
        readonly Func<IActivityMonitor, Type, bool> _actualPocoPredicate;

        /// <summary>
        /// Initializes a new <see cref="PocoDirectoryBuilder"/>.
        /// </summary>
        /// <param name="actualPocoPredicate">
        /// This must be true for actual IPoco interfaces: when false, "base interface" are not directly registered.
        /// This implements the <see cref="CKTypeDefinerAttribute"/> behavior.
        /// </param>
        /// <param name="namespace">Namespace into which dynamic types will be created.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        public PocoDirectoryBuilder( Func<IActivityMonitor, Type, bool> actualPocoPredicate,
                                     string @namespace = "CK.GPoco",
                                     Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            Throw.CheckNotNullArgument( actualPocoPredicate );
            Throw.CheckNotNullArgument( @namespace );
            _actualPocoPredicate = actualPocoPredicate;
            _namespace = @namespace;
            _all = new Dictionary<Type, InterfaceEntry?>();
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

        InterfaceEntry? DoRegisterInterface( IActivityMonitor monitor, Type t )
        {
            Debug.Assert( t.IsInterface && _actualPocoPredicate( monitor, t ) );
            if( !_all.TryGetValue( t, out var p ) )
            {
                p = TryCreateInterfaceEntry( monitor, t );
                _all.Add( t, p );
                if( p != null && p.Root == p ) _result.Add( p.RootCollector! );
            }
            return p;
        }

        InterfaceEntry? TryCreateInterfaceEntry( IActivityMonitor monitor, Type t )
        {
            if( !_typeFilter( monitor, t ) )
            {
                monitor.Warn( $"Poco interface '{t.AssemblyQualifiedName}' is excluded." );
                return null;
            }
            InterfaceEntry? theOnlyRoot = null;
            foreach( Type b in t.GetInterfaces() )
            {
                if( b == typeof( IPoco ) || b == typeof( IClosedPoco ) ) continue;
                // Base interface must be a IPoco. This is a security to avoid "normal" interfaces to appear
                // by mistake in a Poco. This is crucial to be able to correctly handle IPoco interfaces in
                // the whole system.
                if( !typeof( IPoco ).IsAssignableFrom( b ) )
                {
                    monitor.Fatal( $"Poco interface '{t.AssemblyQualifiedName}' extends '{b.Name}'. '{b.Name}' must be marked with CK.Core.IPoco interface." );
                    return null;
                }
                // Attempts to register the base if and only if it is not a "definer".
                if( _actualPocoPredicate( monitor, b ) )
                {
                    var baseType = DoRegisterInterface( monitor, b );
                    // Excluded Poco interfaces are null here. Let's continue.
                    // Errors are detected by a collector on the monitor anyway.
                    if( baseType == null ) continue;
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
            return new InterfaceEntry( t, theOnlyRoot );
        }

        /// <summary>
        /// Finalize registrations by creating a <see cref="IPocoDirectory"/> or null on error.
        /// The <see cref="EmptyPocoDirectory.Default"/> singleton can be used wherever null
        /// references must be avoided.
        /// </summary>
        /// <param name="assembly">The dynamic assembly: its <see cref="IDynamicAssembly.StubModuleBuilder"/> will host the generated stub.</param>
        /// <param name="monitor">Monitor to use.</param>
        /// <returns>The result or null on error.</returns>
        public IPocoDirectory? Build( IDynamicAssembly assembly, IActivityMonitor monitor )
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
            return hasNameError || !r.BuildNameIndex( monitor ) ? null : r;
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

                foreach( var p in i.GetProperties() )
                {
                    // As soon as a property claims to be implemented, we remove it from the PocoProperties.
                    if( p.GetCustomAttributesData().Any( d => d.AttributeType.Name == nameof( AutoImplementationClaimAttribute ) ) )
                    {
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
                        hasPropertyError &= HandlePocoProperty( monitor, properties, propertyList, i, p );
                    }
                    if( !hasPropertyError )
                    {
                        // Always implement the stub as long as there is no error.
                        EmitHelper.ImplementStubProperty( tB, p, isVirtual: false, alwaysImplementSetter: false );
                    }
                }
            }
            if( hasPropertyError ) return null;

            var tPoCo = tB.CreateType();
            Debug.Assert( tPoCo != null );

            var tPocoFactory = tBF.CreateType();
            Debug.Assert( tPocoFactory != null );

            return new PocoRootInfo( tPoCo, tPocoFactory, mustBeClosed, closure, expanded, properties, propertyList, externallyImplementedPropertyList );
        }

        static bool HandlePocoProperty( IActivityMonitor monitor,
                                        Dictionary<string, PocoPropertyInfo> properties,
                                        List<PocoPropertyInfo> propertyList,
                                        Type interfaceType,
                                        PropertyInfo info )
        {
            Debug.Assert( info.DeclaringType == interfaceType );
            // Handle stupidity early.
            if( !info.CanRead )
            {
                monitor.Error( $"Poco property '{interfaceType}.{info.Name}' cannot be read. This is forbidden." );
                return false;
            }
            // Creates the PocoPropertyInfo if this is the first PocoPropertyImpl
            if( !properties.TryGetValue( info.Name, out var pocoProperty ) )
            {
                // New property.
                pocoProperty = new PocoPropertyInfo( propertyList.Count, info.Name );
                properties.Add( info.Name, pocoProperty );
                propertyList.Add( pocoProperty );
            }
            pocoProperty.DeclaredProperties.Add( info );
            return true;
        }

    }
}

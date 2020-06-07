using CK.Core;
using CK.Reflection;
using CK.Setup;
using CK.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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
                    RootCollector = new List<Type>(){ type };
                }
            }
        }

        readonly Dictionary<Type, PocoType?> _all;
        readonly List<List<Type>> _result;
        readonly string _namespace;
        readonly Func<IActivityMonitor, Type, bool> _typeFilter;
        readonly Func<IActivityMonitor, Type, bool> _actualPocoPredicate;
        int _uniqueNumber;

        /// <summary>
        /// Initializes a new <see cref="PocoRegisterer"/>.
        /// </summary>
        /// <param name="actualPocoPredicate">
        /// This must be true for actual IPoco interfaces: when false, "base interface" are not directly registered.
        /// This implements the <see cref="CKTypeDefinerAttribute"/> behavior.
        /// </param>
        /// <param name="namespace">Namespace into which dynamic types will be created.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        public PocoRegisterer( Func<IActivityMonitor, Type, bool> actualPocoPredicate, string @namespace = "CK._g.poco", Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            _actualPocoPredicate = actualPocoPredicate ?? throw new ArgumentNullException( nameof( actualPocoPredicate ) );
            _namespace = @namespace ?? "CK._g.poco";
            _all = new Dictionary<Type, PocoType?>();
            _result = new List<List<Type>>();
            _typeFilter = typeFilter ?? ((m,type) => true);
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
        /// Finalize registrations by creating emiting a <see cref="IPocoSupportResult"/> or null on error.
        /// The <see cref="Empty"/> singleton can be used wherever null references must be avoided.
        /// </summary>
        /// <param name="moduleB">The module builder into which dynamic code is generated.</param>
        /// <param name="monitor">Monitor to use.</param>
        /// <returns>The result or null on error.</returns>
        public IPocoSupportResult? Finalize( ModuleBuilder moduleB, IActivityMonitor monitor )
        {
            _uniqueNumber = 0;
            var tB = moduleB.DefineType( _namespace + ".Factory" );
            Result? r = CreateResult( moduleB, monitor, tB );
            if( r == null ) return null;
            ImplementFactories( r );
            Type? final = tB.CreateType();
            if( final == null )
            {
                monitor.Fatal( $"Final CreateType call returned a null type." );
                return null;
            }
            r.FinalFactory = final;
            return r;
        }

        void ImplementFactories( Result r )
        {
            foreach( var cInfo in r.Roots )
            {
                var g = cInfo.StaticMethod.GetILGenerator();
                g.Emit( OpCodes.Newobj, cInfo.PocoClass.GetConstructor( Type.EmptyTypes )! );
                g.Emit( OpCodes.Ret );
            }
        }

        Result? CreateResult( ModuleBuilder moduleB, IActivityMonitor monitor, TypeBuilder tB )
        {
            MethodInfo typeFromToken = typeof( Type ).GetMethod( nameof( Type.GetTypeFromHandle ), BindingFlags.Static | BindingFlags.Public )!;
            Result r = new Result();
            int idMethod = 0;
            foreach( var signature in _result )
            {
                (Type? tPoco, HashSet<Type>? expanded, bool mustBeClosed, Type? closureInterface) = CreatePocoType( moduleB, monitor, signature );
                if( tPoco == null ) return null;
                Debug.Assert( expanded != null, "Since we have a type." );
                MethodBuilder realMB = tB.DefineMethod( "DoC" + r.Roots.Count.ToString(), MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static, tPoco, Type.EmptyTypes );

                var cInfo = new ClassInfo( tPoco, realMB, mustBeClosed, closureInterface, expanded );
                r.Roots.Add( cInfo );
                foreach( var i in signature )
                {
                    Type iCreate = typeof( IPocoFactory<> ).MakeGenericType( i );
                    tB.AddInterfaceImplementation( iCreate );
                    {
                        MethodBuilder mB = tB.DefineMethod( "C" + (idMethod++).ToString(), MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final, i, Type.EmptyTypes );
                        ILGenerator g = mB.GetILGenerator();
                        g.Emit( OpCodes.Call, realMB );
                        g.Emit( OpCodes.Ret );
                        tB.DefineMethodOverride( mB, iCreate.GetMethod( nameof( IPocoFactory<IPoco>.Create ) )! );
                    }
                    {
                        MethodBuilder mB = tB.DefineMethod( "get_T" + (idMethod++).ToString(), MethodAttributes.Virtual | MethodAttributes.Private | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Final, typeof(Type), Type.EmptyTypes );
                        ILGenerator g = mB.GetILGenerator();
                        g.Emit( OpCodes.Ldtoken, tPoco );
                        g.Emit( OpCodes.Call, typeFromToken );
                        g.Emit( OpCodes.Ret );
                        tB.DefineMethodOverride( mB, iCreate.GetProperty( nameof( IPocoFactory<IPoco>.PocoClassType ) )!.GetGetMethod()! );
                    }
                    var iInfo = new InterfaceInfo( cInfo, i, iCreate );
                    cInfo.Interfaces.Add( iInfo );
                    r.AllInterfaces.Add( i, iInfo );
                }
                Debug.Assert( expanded != null );
                expanded.Remove( typeof( IPoco ) );
                expanded.Remove( typeof( IClosedPoco ) );
                foreach( var t in signature ) expanded.Remove( t );
                foreach( var e in expanded )
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
            }
            return r;
        }

        (Type? created, HashSet<Type>? rawInterfaces, bool mustBeClosed, Type? closureInterface) CreatePocoType( ModuleBuilder moduleB, IActivityMonitor monitor, IReadOnlyList<Type> interfaces )
        {
            var tB = moduleB.DefineType( $"{_namespace}.Poco{_uniqueNumber++}" );
            var properties = new Dictionary<string, PropertyInfo>();

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
            }
            if( mustBeClosed )
            {
                Debug.Assert( maxICount < expanded.Count );
                if( maxICount < expanded.Count - 1 )
                {
                    monitor.Error( $"Poco family '{interfaces.Select( b => b.FullName ).Concatenate("', '")}' must be closed but none of these interfaces covers the other ones." );
                    return (null, null, false, null);
                }
                else
                {
                    Debug.Assert( closure != null, "Since there is at least one interface." );
                    monitor.Debug( $"{closure.FullName}: IClosedPoco for {interfaces.Select( b => b.FullName ).Concatenate()}." );
                }
            }
            foreach( var i in expanded )
            {
                tB.AddInterfaceImplementation( i );
                foreach( var p in i.GetProperties() )
                {
                    // Nullable and language definition here: the out parameter type is necessarily optional because of:
                    //  - the MaybeNullWhen attribute of: bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
                    //  - the inline parameter declaration is in the scope above the if statement.
                    if( properties.TryGetValue( p.Name, out PropertyInfo? implP ) )
                    {
                        // However, since TrGetValue returned true, the magic happens: implP is not null.  
                        if( implP.PropertyType != p.PropertyType )
                        {
                            monitor.Error( $"Interface '{i}' and '{implP.DeclaringType}' both declare property '{p.Name}' but their type differ ({p.PropertyType.Name} vs. {implP.PropertyType.Name})." );
                            return (null, null, false, null);
                        }
                    }
                    else
                    {
                        EmitHelper.ImplementStubProperty( tB, p, false, true );
                        properties.Add( p.Name, p );
                    }
                }
            }
            return (tB.CreateType(), expanded, mustBeClosed, closure);
        }
    }
}

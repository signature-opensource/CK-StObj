
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CK.Reflection;
using CK.CodeGen;
using System.Reflection.Emit;
using CK.Core;
using CK.Setup;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Handles abstract Type and <see cref="IAutoImplementationClaimAttribute"/>, <see cref="IAutoImplementorMethod"/>
    /// and <see cref="IAutoImplementorProperty"/> members.
    /// </summary>
    public class ImplementableTypeInfo
    {
        /// <summary>
        /// Marker type exposed by <see cref="UnimplementedMarker"/>.
        /// </summary>
        public class NoImplementationMarker : IAutoImplementorMethod, IAutoImplementorProperty
        {
            CSCodeGenerationResult IAutoImplementor<MethodInfo>.Implement( IActivityMonitor monitor, MethodInfo m, ICSCodeGenerationContext c, ITypeScope b )
            {
                throw new NotSupportedException();
            }

            CSCodeGenerationResult IAutoImplementor<PropertyInfo>.Implement( IActivityMonitor monitor, PropertyInfo p, ICSCodeGenerationContext c, ITypeScope b )
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Exposes <see cref="IAutoImplementorMethod"/> and <see cref="IAutoImplementorProperty"/> that implement
        /// <see cref="NotSupportedException"/>) behaviors since this marker is not intended to be used.
        /// </summary>
        public static readonly NoImplementationMarker UnimplementedMarker = new NoImplementationMarker();

        Type? _stubType;

        /// <summary>
        /// Gets the starting type that must be automatically implemented.
        /// </summary>
        public Type AbstractType { get; }

        /// <summary>
        /// Gets the <see cref="ICSCodeGeneratorType"/>.
        /// </summary>
        public IReadOnlyList<ICSCodeGeneratorType> TypeImplementors { get; }

        /// <summary>
        /// Gets the current property information for all abstract or virtual properties of the <see cref="AbstractType"/>.
        /// </summary>
        public IReadOnlyList<ImplementablePropertyInfo> PropertiesToImplement { get; }

        /// <summary>
        /// Gets the current method information for all abstract or virtual methods of the <see cref="AbstractType"/>.
        /// </summary>
        public IReadOnlyList<ImplementableMethodInfo> MethodsToImplement { get; }

        /// <summary>
        /// Gets the stub type. Null if <see cref="CreateStubType"/> has not been called yet.
        /// </summary>
        public Type? StubType => _stubType;

        ImplementableTypeInfo( Type t, ICSCodeGeneratorType[] typeImplementor, IReadOnlyList<ImplementablePropertyInfo> p, IReadOnlyList<ImplementableMethodInfo> m )
        {
            AbstractType = t;
            PropertiesToImplement = p;
            MethodsToImplement = m;
            TypeImplementors = typeImplementor;
        }

        /// <summary>
        /// Attempts to create a new <see cref="ImplementableTypeInfo"/>.
        /// <para>
        /// </para>
        /// If the type is marked with <see cref="PreventAutoImplementationAttribute"/> or that the type misses
        /// a <see cref="IAutoImplementationClaimAttribute"/> (or the <see cref="AutoImplementationClaimAttribute"/>) and one of
        /// its abstract methods (or properties) misses <see cref="IAutoImplementationClaimAttribute"/> or <see cref="IAutoImplementorMethod"/> (<see cref="IAutoImplementorProperty"/>
        /// for properties), null is returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="abstractType">Abstract type to automatically implement if possible.</param>
        /// <param name="attributeProvider">Attributes provider that will be used.</param>
        /// <returns>An instance of <see cref="ImplementableTypeInfo"/> or null if the type is not automatically implementable.</returns>
        static public ImplementableTypeInfo? CreateImplementableTypeInfo( IActivityMonitor monitor, Type abstractType, ICKCustomAttributeProvider attributeProvider )
        {
            static bool HasAutoImplementationClaim( ICKCustomAttributeProvider attributeProvider, MemberInfo m )
            {
                return attributeProvider.IsDefined( m, typeof( IAutoImplementationClaimAttribute ) )
                       || m.GetCustomAttributesData().Any( d => d.AttributeType.Name == nameof( AutoImplementationClaimAttribute ) );
            }

            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( abstractType == null ) throw new ArgumentNullException( nameof( abstractType ) );
            if( !abstractType.IsClass || !abstractType.IsAbstract ) throw new ArgumentException( "Type must be an abstract class.", nameof( abstractType ) );
            if( attributeProvider == null ) throw new ArgumentNullException( nameof( attributeProvider ) );

            if( abstractType.GetCustomAttributesData().Any( d => d.AttributeType.Name == nameof( PreventAutoImplementationAttribute ) ) )
            {
                monitor.Trace( $"Type {abstractType} is marked with a [{nameof( PreventAutoImplementationAttribute )}]. Auto implementation is skipped." );
                return null;
            }
            // Gets whether the Type itself is marked with an attribute that claims that the type is handled.
            bool isTypeAutoImplemented = HasAutoImplementationClaim( attributeProvider, abstractType );

            ICSCodeGeneratorType[] typeImplementors = attributeProvider.GetCustomAttributes<ICSCodeGeneratorType>( abstractType ).ToArray();

            // Gets all the virtual methods (abstract methods are virtual).
            var candidates = abstractType.GetMethods( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public )
                                         .Where( m => !m.IsSpecialName && m.IsVirtual );
            int nbUncovered = 0;
            List<ImplementableMethodInfo> methods = new List<ImplementableMethodInfo>();
            foreach( var m in candidates )
            {
                bool isAbstract = m.IsAbstract;
                if( isAbstract ) ++nbUncovered;
                // First, consider any IAutoImplementorMethod attribute.
                IAutoImplementorMethod? impl = attributeProvider.GetCustomAttributes<IAutoImplementorMethod>( m ).SingleOrDefault();
                if( impl == null )
                {
                    // Second, ask the type.
                    IAutoImplementorMethod? tImpl = typeImplementors.Select( t => t.HandleMethod( monitor, m ) ).FirstOrDefault( i => i != null );
                    if( (impl = tImpl) == null )
                    {
                        // Third, in case of an abstract method, use the ultimate workaround to avoid an error right now
                        // and defer the resolution (if possible).
                        if( isAbstract && (isTypeAutoImplemented || HasAutoImplementationClaim( attributeProvider, m )) ) impl = UnimplementedMarker;
                    }
                }
                if( impl != null )
                {
                    if( isAbstract ) --nbUncovered;
                    methods.Add( new ImplementableMethodInfo( m, impl ) );
                }
                else
                {
                    if( isAbstract ) monitor.Warn( $"Unable to find auto implementor for abstract method {m.DeclaringType}.{m.Name}." );
                }
            }
            List<ImplementablePropertyInfo> properties = new List<ImplementablePropertyInfo>();
            var pCandidates = abstractType.GetProperties( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public );
            foreach( var p in pCandidates )
            {
                MethodInfo? mGet = p.GetGetMethod( true );
                MethodInfo? mSet = p.GetSetMethod( true );

                // Okay... This is awful but it seems that there's no other quick way to detect an explicit method
                // implementation... This works for C# because the name mangling is deterministic: the method name
                // starts with the interface full name followed by '.' method name. Since a dot in a "regular" method
                // name is not possible, this does the job...
                bool isVirtual = (mGet != null && mGet.IsVirtual && !mGet.Name.Contains('.'))
                                 || (mSet != null && mSet.IsVirtual && mSet.Name.Contains( '.' ));
                if( !isVirtual ) continue;

                bool isAbstract = (mGet != null && mGet.IsAbstract) || (mSet != null && mSet.IsAbstract);
                if( isAbstract ) ++nbUncovered;
                // First, consider any IAutoImplementorMethod attribute.
                IAutoImplementorProperty? impl = attributeProvider.GetCustomAttributes<IAutoImplementorProperty>( p ).SingleOrDefault();
                if( impl == null )
                {
                    // Second, ask the type.
                    IAutoImplementorProperty? tImpl = typeImplementors.Select( t => t.HandleProperty( monitor, p ) ).FirstOrDefault( i => i != null );
                    if( (impl = tImpl) == null )
                    {
                        // Third, in case of an abstract method, use the ultimate workaround to avoid an error right now and defer the resolution (if possible).
                        if( isAbstract && (isTypeAutoImplemented || HasAutoImplementationClaim( attributeProvider, p )) ) impl = UnimplementedMarker;
                    }
                }
                if( impl != null )
                {
                    if( isAbstract ) --nbUncovered;
                    properties.Add( new ImplementablePropertyInfo( p, impl ) );
                }
                else
                {
                    if( isAbstract ) monitor.Warn( $"Unable to find auto implementor for abstract property {p.DeclaringType}.{p.Name}." );
                }
            }
            if( nbUncovered > 0 )
            {
                // We are missing something.
                return null;
            }
            return new ImplementableTypeInfo( abstractType, typeImplementors, properties, methods );
        }

        /// <summary>
        /// Implements the <see cref="StubType"/> in a dynamic assembly that specializes <see cref="AbstractType"/> and returns it.
        /// </summary>
        /// <param name="monitor">Logger to use.</param>
        /// <param name="assembly">Dynamic assembly.</param>
        /// <returns>The newly created type in the dynamic assembly. Null if an error occurred.</returns>
        public Type? CreateStubType( IActivityMonitor monitor, IDynamicAssembly assembly )
        {
            if( _stubType != null ) Throw.InvalidOperationException( "Must be called only if StubType is null." );
            try
            {
                TypeAttributes tA = TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed;
                TypeBuilder b = assembly.StubModuleBuilder.DefineType( assembly.GetAutoImplementedTypeName( AbstractType ), tA, AbstractType );
                // Relayed constructors replicates all their potential attributes (except attributes on parameters).
                b.DefinePassThroughConstructors( c => c.Attributes | MethodAttributes.Public, null, (param,attributeData) => false );
                foreach( var am in MethodsToImplement )
                {
                    CK.Reflection.EmitHelper.ImplementEmptyStubMethod( b, am.Method, false );
                }
                foreach( var ap in PropertiesToImplement )
                {
                    CK.Reflection.EmitHelper.ImplementStubProperty( b, ap.Property, false );
                }
                return _stubType = b.CreateType();
            }
            catch( Exception ex )
            {
                monitor.Fatal( $"While implementing Stub for '{AbstractType.FullName}'.", ex );
                return null;
            }
        }

        /// <summary>
        /// Generates the code of this Type.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The target dynamic assembly.</param>
        /// <param name="secondPass">The second pass collector.</param>
        public void RunFirstPass( IActivityMonitor monitor, ICSCodeGenerationContext c, List<MultiPassCodeGeneration> secondPass )
        {
            if( _stubType == null ) Throw.InvalidOperationException( $"StubType not available for '{AbstractType.Name}'." );

            ITypeScope cB = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, _stubType );

            // Calls all Type level implementors first.
            foreach( var impl in TypeImplementors )
            {
                var second = MultiPassCodeGeneration.FirstPass( monitor, impl, c, cB, AbstractType ).SecondPass;
                if( second != null ) secondPass.Add( second );
            }
            // Calls all method implementors.
            foreach( var am in MethodsToImplement )
            {
                IAutoImplementorMethod m = am.ImplementorToUse;
                if( m != UnimplementedMarker )
                {
                    if( m == null )
                    {
                        monitor.Fatal( $"Method '{AbstractType}.{am.Method.Name}' has no valid associated IAutoImplementorMethod." );
                    }
                    else
                    {
                        var second = MultiPassCodeGeneration.FirstPass( monitor, m, c, cB, am.Method ).SecondPass;
                        if( second != null ) secondPass.Add( second );
                    }
                }
            }
            // Finishes with all property implementors.
            foreach( var ap in PropertiesToImplement )
            {
                IAutoImplementorProperty p = ap.ImplementorToUse;
                if( p != UnimplementedMarker )
                {
                    if( p == null )
                    {
                        monitor.Fatal( $"Property '{AbstractType}.{ap.Property.Name}' has no valid associated IAutoImplementorProperty." );
                    }
                    else
                    {
                        var second = MultiPassCodeGeneration.FirstPass( monitor, p, c, cB, ap.Property ).SecondPass;
                        if( second != null ) secondPass.Add( second );
                    }
                }
            }
        }

        /// <summary>
        /// Overridden to return a readable string with the <see cref="AbstractType"/> name and the <see cref="StubType"/> if there is one.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{AbstractType.Name} => {_stubType?.Name ?? "(no stub type)" }";

    }
}

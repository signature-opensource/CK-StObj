
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CK.Reflection;
using CK.CodeGen;
using CK.CodeGen.Abstractions;
using System.Reflection.Emit;
using CK.Core;
using CK.Text;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Handles abstract Type and <see cref="IAttributeAutoImplemented"/>, <see cref="IAutoImplementorMethod"/>
    /// and <see cref="IAutoImplementorProperty"/> members.
    /// </summary>
    public class ImplementableTypeInfo
    {
        /// <summary>
        /// Marker type exposed by <see cref="UnimplementedMarker"/>.
        /// </summary>
        public class NoImplementationMarker : IAutoImplementorMethod, IAutoImplementorProperty
        {
            bool IAutoImplementorMethod.Implement( IActivityMonitor monitor, MethodInfo m, ICodeGenerationContext c, ITypeScope b )
            {
                throw new NotSupportedException();
            }

            bool IAutoImplementorProperty.Implement( IActivityMonitor monitor, PropertyInfo p, ICodeGenerationContext c, ITypeScope b )
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
        public readonly Type AbstractType;

        /// <summary>
        /// Gets the <see cref="IAutoImplementorType"/>.
        /// </summary>
        public readonly IReadOnlyList<IAutoImplementorType> TypeImplementors;

        /// <summary>
        /// Gets the current property information for all abstract properties of the <see cref="AbstractType"/>.
        /// </summary>
        public readonly IReadOnlyList<ImplementableAbstractPropertyInfo> PropertiesToImplement;

        /// <summary>
        /// Gets the current method information for all abstract methods of the <see cref="AbstractType"/>.
        /// </summary>
        public readonly IReadOnlyList<ImplementableAbstractMethodInfo> MethodsToImplement;

        /// <summary>
        /// Gets the stub type. Null if <see cref="CreateStubType"/> has not been called yet.
        /// </summary>
        public Type? StubType => _stubType;

        ImplementableTypeInfo( Type t, IAutoImplementorType[] typeImplementor, IReadOnlyList<ImplementableAbstractPropertyInfo> p, IReadOnlyList<ImplementableAbstractMethodInfo> m )
        {
            AbstractType = t;
            PropertiesToImplement = p;
            MethodsToImplement = m;
            TypeImplementors = typeImplementor;
        }

        /// <summary>
        /// Attempts to create a new <see cref="ImplementableTypeInfo"/>. If the type is marked with <see cref="PreventAutoImplementationAttribute"/> or that one
        /// of its abstract methods (or properties) misses <see cref="IAttributeAutoImplemented"/> or <see cref="IAutoImplementorMethod"/> (<see cref="IAutoImplementorProperty"/>
        /// for properties), null is returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="abstractType">Abstract type to automatically implement if possible.</param>
        /// <param name="attributeProvider">Attributes provider that will be used.</param>
        /// <returns>An instance of <see cref="ImplementableTypeInfo"/> or null if the type is not automatically implementable.</returns>
        static public ImplementableTypeInfo? CreateImplementableTypeInfo( IActivityMonitor monitor, Type abstractType, ICKCustomAttributeProvider attributeProvider )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( abstractType == null ) throw new ArgumentNullException( nameof( abstractType ) );
            if( !abstractType.IsClass || !abstractType.IsAbstract ) throw new ArgumentException( "Type must be an abstract class.", nameof( abstractType ) );
            if( attributeProvider == null ) throw new ArgumentNullException( nameof( attributeProvider ) );

            if( abstractType.GetCustomAttributesData().Any( d => d.AttributeType.Name == "PreventAutoImplementationAttribute" ) )
            {
                monitor.Trace( $"Type {abstractType} is marked with a [PreventAutoImplementationAttribute]. Auto implementation is skipped." );
                return null;
            }

            IAutoImplementorType[] typeImplementors = attributeProvider.GetCustomAttributes<IAutoImplementorType>( abstractType ).ToArray();

            var candidates = abstractType.GetMethods( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public ).Where( m => !m.IsSpecialName && m.IsAbstract );
            int nbUncovered = 0;
            List<ImplementableAbstractMethodInfo> methods = new List<ImplementableAbstractMethodInfo>();
            foreach( var m in candidates )
            {
                ++nbUncovered;
                // First, consider any IAutoImplementorMethod attribute.
                IAutoImplementorMethod? impl = attributeProvider.GetCustomAttributes<IAutoImplementorMethod>( m ).SingleOrDefault();
                if( impl == null )
                {
                    // Second, ask the type.
                    var tImpl = typeImplementors.Select( t => t.HandleMethod( monitor, m ) ).FirstOrDefault( i => i != null );
                    if( (impl = tImpl) == null )
                    {
                        // Third, use the ultimate workaround to avoid an error right now and defer the resolution (if possible).
                        if( attributeProvider.IsDefined( m, typeof( IAttributeAutoImplemented ) ) ) impl = UnimplementedMarker;
                    }
                }
                if( impl != null )
                {
                    --nbUncovered;
                    methods.Add( new ImplementableAbstractMethodInfo( m, impl ) );
                }
                else
                {
                    monitor.Warn( $"Unable to find auto implementor for abstract method {m.DeclaringType}.{m.Name}." );
                }
            }
            List<ImplementableAbstractPropertyInfo> properties = new List<ImplementableAbstractPropertyInfo>();
            var pCandidates = abstractType.GetProperties( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public );
            foreach( var p in pCandidates )
            {
                MethodInfo? mGet = p.GetGetMethod( true );
                MethodInfo? mSet = p.GetSetMethod( true );
                bool isAbstract = (mGet != null && mGet.IsAbstract) || (mSet != null && mSet.IsAbstract);
                if( isAbstract )
                {
                    ++nbUncovered;
                    // First, consider any IAutoImplementorMethod attribute.
                    IAutoImplementorProperty? impl = attributeProvider.GetCustomAttributes<IAutoImplementorProperty>( p ).SingleOrDefault();
                    if( impl == null )
                    {
                        // Second, ask the type.
                        var tImpl = typeImplementors.Select( t => t.HandleProperty( monitor, p ) ).FirstOrDefault( i => i != null );
                        if( (impl = tImpl) == null )
                        {
                            // Third, use the ultimate workaround to avoid an error right now and defer the resolution (if possible).
                            if( attributeProvider.IsDefined( p, typeof( IAttributeAutoImplemented ) ) ) impl = UnimplementedMarker;
                        }
                    }
                    if( impl != null )
                    {
                        --nbUncovered;
                        properties.Add( new ImplementableAbstractPropertyInfo( p, impl ) );
                    }
                    else
                    {
                        monitor.Warn( $"Unable to find auto implementor for abstract property {p.DeclaringType}.{p.Name}." );
                    }
                }
            }
            if( nbUncovered > 0 ) return null;
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
            if( _stubType != null ) throw new InvalidOperationException( "Must be called only if StubType is null." );
            try
            {
                TypeAttributes tA = TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed;
                TypeBuilder b = assembly.StubModuleBuilder.DefineType( assembly.AutoNextTypeName( AbstractType.Name ), tA, AbstractType );
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
        /// Generates the code of this Type and returns its full name.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="a">The target dynamic assembly.</param>
        /// <returns>The full name of the generated type.</returns>
        public string GenerateType( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            if( _stubType == null ) throw new InvalidOperationException( $"StubType not available for '{AbstractType.Name}'." );

            // Currently there is no way to alter the class attributes (intrinsic as well as custom), nor adding
            // interfaces, nor modifying/removing "pass through" constructors.
            // This may be added if needed by extending IAutoImplementorType interface and
            // by complexifying the code below a little bit or (better?) by adding ITypeScopeParts (like "BaseTypes") exposed by a ITypeScope...
            ITypeScope cB = c.Assembly.DefaultGenerationNamespace.CreateType( t => t.Append( "public class " )
                                                                           .Append( _stubType.Name )
                                                                           .Append( " : " )
                                                                           .AppendCSharpName( AbstractType ) );
            cB.AppendPassThroughConstructors( AbstractType );

            // Calls all Type level implementors first.
            foreach( var impl in TypeImplementors )
            {
                if( !impl.Implement( monitor, AbstractType, c, cB ) )
                {
                    monitor.Fatal( $"Type implementor '{impl.GetType().Name}' failed to implement type '{AbstractType}'." );
                }
            }
            // Calls all method implementors.
            foreach( var am in MethodsToImplement )
            {
                IAutoImplementorMethod m = am.ImplementorToUse;
                if( m == null || m == UnimplementedMarker )
                {
                    monitor.Fatal( $"Method '{AbstractType}.{am.Method.Name}' has no valid associated IAutoImplementorMethod." );
                }
                else
                {
                    if( !m.Implement( monitor, am.Method, c, cB ) )
                    {
                        monitor.Fatal( $"Method '{AbstractType}.{am.Method.Name}' can not be implemented by its IAutoImplementorMethod." );
                    }
                }
            }
            // Finishes with all property implementors.
            foreach( var ap in PropertiesToImplement )
            {
                IAutoImplementorProperty p = ap.ImplementorToUse;
                if( p == null || p == UnimplementedMarker )
                {
                    monitor.Fatal( $"Property '{AbstractType}.{ap.Property.Name}' has no valid associated IAutoImplementorProperty." );
                }
                else
                {
                    if( !p.Implement( monitor, ap.Property, c, cB ) )
                    {
                        monitor.Fatal( $"Property '{AbstractType}.{ap.Property.Name}' can not be implemented by its IAutoImplementorProperty." );
                    }
                }
            }
            return cB.FullName;
        }

        /// <summary>
        /// Overridden to return a readable string with the <see cref="AbstractType"/> name and the <see cref="StubType"/> if there is one.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{AbstractType.Name} => {_stubType?.Name ?? "(no stub type)" }";

    }
}

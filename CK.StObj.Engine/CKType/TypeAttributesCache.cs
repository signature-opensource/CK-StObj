using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Implements a cache for attributes associated to a type or to any of its members that
    /// support <see cref="IAttributeContextBound"/>.
    /// Attribute inheritance is ignored: only attributes applied to the member are considered. 
    /// When used with another type or a member of another type from the one provided 
    /// in the constructor, an exception is thrown.
    /// <para>
    /// TODO: this should be heavily re-factored as a ubiquitous type cache with <see cref="IExtMemberInfo"/>.
    /// A IServiceCollection/IServicePorvider should be central and configured with the aspects: instantiation
    /// of the attribute implementation must benefit of the central provider.
    /// I can't say whether this is a heavy or easily doable refactoring without trying :(.
    /// </para>
    /// </summary>
    public class TypeAttributesCache : ITypeAttributesCache
    {
        readonly struct Entry
        {
            public Entry( MemberInfo m, object a )
            {
                M = m;
                Attr = a;
            }

            public readonly MemberInfo M;
            public readonly object Attr;
        }
        readonly Entry[] _all;
        readonly MemberInfo[] _typeMembers;
        readonly bool _includeBaseClasses;

        /// <summary>
        /// Initializes a new <see cref="TypeAttributesCache"/> that can consider only members explicitly 
        /// declared by the <paramref name="type"/> or includes the base class.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="type">Type for which attributes must be cached.</param>
        /// <param name="services">Available services that will be used for delegated attribute constructor injection.</param>
        /// <param name="includeBaseClass">True to include attributes of base classes and attributes on members of the base classes.</param>
        /// <param name="alsoRegister">Enables a <see cref="IAttributeContextBoundInitializer.Initialize"/> to register types (typically nested types).</param>
        public TypeAttributesCache( IActivityMonitor monitor, Type type, IServiceProvider services, bool includeBaseClass, Action<Type> alsoRegister )
            : this( monitor,
                    type,
                    (IAttributeContextBound[])type.GetCustomAttributes( typeof( IAttributeContextBound ), includeBaseClass ),
                    services,
                    includeBaseClass,
                    alsoRegister )
        {
        }

        TypeAttributesCache( IActivityMonitor monitor,
                             Type type,
                             IAttributeContextBound[] typeAttributes,
                             IServiceProvider services,
                             bool includeBaseClasses,
                             Action<Type> alsoRegister )
        {
            Throw.CheckNotNullArgument( type );

            // This is ready to be injected in the delegated attribute constructor: no other attributes are visible.
            // If other attributes must be accessed, then the IAttributeContextBoundInitializer interface must be used.
            Type = type;
            _all = Array.Empty<Entry>();

            var all = new List<Entry>();
            int initializerCount = Register( monitor, services, all, type, includeBaseClasses, typeAttributes );
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            if( includeBaseClasses ) flags &= ~BindingFlags.DeclaredOnly;
            _typeMembers = type.GetMembers( flags );
            foreach( var m in _typeMembers ) initializerCount += Register( monitor, services, all, m, false );
            _all = all.ToArray();
            _includeBaseClasses = includeBaseClasses;
            if( initializerCount > 0 )
            {
                foreach( Entry e in _all )
                {
                    if( e.Attr is IAttributeContextBoundInitializer aM )
                    {
                        aM.Initialize( monitor, this, e.M, alsoRegister );
                        if( --initializerCount == 0 ) break;
                    }
                }
            }
        }

        int Register( IActivityMonitor monitor,
                      IServiceProvider services,
                      List<Entry> all,
                      MemberInfo m,
                      bool includeBaseClass,
                      IAttributeContextBound[]? alreadyKnownMemberAttributes = null )
        {
            int initializerCount = 0;
            var attr = alreadyKnownMemberAttributes
                       ?? (IAttributeContextBound[])m.GetCustomAttributes( typeof( IAttributeContextBound ), includeBaseClass );
            foreach( var a in attr )
            {
                object? finalAttributeToUse = a;
                if( a is ContextBoundDelegationAttribute delegated )
                {
                    Type? dT = SimpleTypeFinder.WeakResolver( delegated.ActualAttributeTypeAssemblyQualifiedName, true );
                    Debug.Assert( dT != null );
                    // When ContextBoundDelegationAttribute is not specialized, it is useless: the attribute
                    // parameter must not be specified.
                    using( var sLocal = new SimpleServiceContainer( services ) )
                    {
                        Debug.Assert( _all.Length == 0, "Constructors see no attributes at all. IAttributeContextBoundInitializer must be used to have access to other attributes." );
                        sLocal.Add( monitor );
                        sLocal.Add( Type );
                        sLocal.Add<ITypeAttributesCache>( this );
                        sLocal.Add<MemberInfo>( m );
                        if( m is MethodInfo method ) sLocal.Add<MethodInfo>( method );
                        else if( m is PropertyInfo property ) sLocal.Add<PropertyInfo>( property );
                        else if( m is FieldInfo field ) sLocal.Add<FieldInfo>( field );
                        finalAttributeToUse = a.GetType() == typeof( ContextBoundDelegationAttribute )
                                            ? sLocal.SimpleObjectCreate( monitor, dT )
                                            : sLocal.SimpleObjectCreate( monitor, dT, a );
                    }
                    if( finalAttributeToUse == null ) continue;
                }
                all.Add( new Entry( m, finalAttributeToUse ) );
                if( finalAttributeToUse is IAttributeContextBoundInitializer ) ++initializerCount;
            }
            return initializerCount;
        }

        /// <summary>
        /// Creates a cache only if at least one <see cref="IAttributeContextBound"/> exists on the type.
        /// If such an attribute exists, all its members are handled as usual.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="services">Available services that will be used for delegated attribute constructor injection.</param>
        /// <param name="type">Type for which attributes must be cached.</param>
        /// <param name="alsoRegister">Enables a <see cref="IAttributeContextBoundInitializer.Initialize"/> to register types (typically nested types).</param>
        /// <returns>The cache or null.</returns>
        public static TypeAttributesCache? CreateOnRegularType( IActivityMonitor monitor, IServiceProvider services, Type type, Action<Type> alsoRegister )
        {
            var attr = (IAttributeContextBound[])type.GetCustomAttributes( typeof( IAttributeContextBound ), false );
            return attr.Length > 0 ? new TypeAttributesCache( monitor, type, attr, services, false, alsoRegister ) : null;
        }

        /// <summary>
        /// Get the Type that is managed by this cache.
        /// </summary>
        public Type Type { get; } 

        /// <summary>
        /// Gets all <see cref="MemberInfo"/> that this <see cref="ICKCustomAttributeMultiProvider"/> handles.
        /// The <see cref="Type"/> is appended to this list.
        /// </summary>
        /// <returns>Enumeration of members.</returns>
        public IEnumerable<MemberInfo> GetMembers() => _typeMembers.Append( Type );

        /// <summary>
        /// Gets whether an attribute that is assignable to the given <paramref name="attributeType"/> 
        /// exists on the given member.
        /// </summary>
        /// <param name="m">The member.</param>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>True if at least one attribute exists.</returns>
        public bool IsDefined( MemberInfo m, Type attributeType )
        {
            Throw.CheckNotNullArgument( m );
            Throw.CheckNotNullArgument( "Members must always be retrieved through its DeclaringType.", m.DeclaringType == m.ReflectedType );
            Throw.CheckNotNullArgument( attributeType );
            return _all.Any( e => e.M == m && attributeType.IsAssignableFrom( e.Attr.GetType() ) )
                    || ( (m.DeclaringType == Type || (_includeBaseClasses && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom( Type ))) 
                         && m.GetCustomAttributes(false).Any( a => attributeType.IsAssignableFrom( a.GetType()) ) );
        }

        /// <summary>
        /// Gets attributes on a <see cref="MemberInfo"/> that are assignable to <paramref name="attributeType"/>.
        /// Instances of attributes that support <see cref="IAttributeContextBound"/> are always the same. 
        /// Other attributes are instantiated (by calling <see cref="MemberInfo.GetCustomAttributes(Type,bool)"/>).
        /// </summary>
        /// <param name="m">Method of <see cref="P:Type"/>.</param>
        /// <param name="attributeType">Type that must be supported by the attributes.</param>
        /// <returns>A set of attributes that are guaranteed to be assignable to <paramref name="attributeType"/>.</returns>
        public IEnumerable<object> GetCustomAttributes( MemberInfo m, Type attributeType )
        {
            Throw.CheckNotNullArgument( m );
            Throw.CheckArgument( "Members must always be retrieved through its DeclaringType.", m.DeclaringType == m.ReflectedType );
            Throw.CheckNotNullArgument( attributeType );
            var fromCache = _all.Where( e => e.M == m && attributeType.IsAssignableFrom( e.Attr.GetType() ) ).Select( e => e.Attr );
            if( m.DeclaringType == Type || (_includeBaseClasses && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom( Type )) )
            {
                return fromCache
                        .Concat( m.GetCustomAttributes( false ).Where( a => !(a is IAttributeContextBound) && attributeType.IsAssignableFrom( a.GetType() ) ) );
            }
            return fromCache;
        }

        /// <summary>
        /// Gets attributes on a <see cref="MemberInfo"/> that are assignable to <typeparamref name="T"/>.
        /// Instances of attributes that support <see cref="IAttributeContextBound"/> are always the same. 
        /// Other attributes are instantiated (by calling <see cref="MemberInfo.GetCustomAttributes(Type,bool)"/>).
        /// </summary>
        /// <typeparam name="T">Type that must be supported by the attributes.</typeparam>
        /// <param name="m">Method of <see cref="P:Type"/>.</param>
        /// <returns>A set of typed attributes.</returns>
        public IEnumerable<T> GetCustomAttributes<T>( MemberInfo m )
        {
            Throw.CheckNotNullArgument( m );
            Throw.CheckNotNullArgument( "Members must always be retrieved through its DeclaringType.", m.DeclaringType == m.ReflectedType );
            var fromCache = _all.Where( e => e.M == m && e.Attr is T ).Select( e => (T)e.Attr );
            if( m.DeclaringType == Type || (_includeBaseClasses && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom( Type )) )
            {
                return fromCache
                        .Concat( m.GetCustomAttributes( false ).Where( a => !(a is IAttributeContextBound) && a is T).Select( a => (T)(object)a ) );
            }
            return fromCache;
        }

        IEnumerable<object> ICKCustomAttributeMultiProvider.GetAllCustomAttributes( Type attributeType )
        {
            return GetAllCustomAttributes( attributeType );
        }

        /// <inheritdoc />
        public IEnumerable<object> GetAllCustomAttributes( Type attributeType, bool memberOnly = false )
        {
            var fromCache = _all.Where( e => (!memberOnly || e.M != Type) && attributeType.IsAssignableFrom( e.Attr.GetType() ) ).Select( e => e.Attr );
            var fromMembers = _typeMembers.SelectMany( m => m.GetCustomAttributes( false ).Where( a => !(a is IAttributeContextBound) && attributeType.IsAssignableFrom( a.GetType() ) ) );
            if( memberOnly ) return fromCache.Concat( fromMembers );
            var fromType = Type.GetCustomAttributes( _includeBaseClasses ).Where( a => !(a is IAttributeContextBound) && attributeType.IsAssignableFrom( a.GetType() ) );
            return fromCache.Concat( fromType ).Concat( fromMembers );
        }

        IEnumerable<T> ICKCustomAttributeMultiProvider.GetAllCustomAttributes<T>() => GetAllCustomAttributes<T>();

        /// <summary>
        /// Gets all attributes that are assignable to the given type, regardless of the <see cref="MemberInfo"/>
        /// that carries it.
        /// </summary>
        /// <typeparam name="T">Type of the attributes.</typeparam>
        /// <param name="memberOnly">True to ignore attributes of the type itself.</param>
        /// <returns>Enumeration of attributes (possibly empty).</returns>
        public IEnumerable<T> GetAllCustomAttributes<T>( bool memberOnly = false)
        {
            var fromCache = _all.Where( e => e.Attr is T && (!memberOnly || e.M != Type) ).Select( e => (T)e.Attr );
            var fromMembers = _typeMembers.SelectMany( m => m.GetCustomAttributes( false ) )
                                            .Where( a => !(a is IAttributeContextBound) && a is T )
                                            .Select( a => (T)(object)a );
            if( memberOnly ) return fromCache.Concat( fromMembers );
            var fromType = Type.GetCustomAttributes( _includeBaseClasses )
                                .Where( a => !(a is IAttributeContextBound) && a is T ).Select( a => (T)(object)a );
            return fromCache.Concat( fromType ).Concat( fromMembers );
        }

        /// <summary>
        /// Gets all <see cref="Type"/>'s attributes that are assignable to the given <paramref name="attributeType"/>.
        /// No attribute members appear.
        /// </summary>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>Enumeration of attributes (possibly empty).</returns>
        public IEnumerable<object> GetTypeCustomAttributes( Type attributeType )
        {
            var fromCache = _all.Where( e => e.M == Type && attributeType.IsAssignableFrom( e.Attr.GetType() ) ).Select( e => e.Attr );
            var fromType = Type.GetCustomAttributes( _includeBaseClasses ).Where( a => !(a is IAttributeContextBound) && attributeType.IsAssignableFrom( a.GetType() ) );
            return fromCache.Concat( fromType );
        }

        /// <summary>
        /// Gets all <see cref="Type"/>'s attributes that are assignable to the given type.
        /// No attribute members appear.
        /// </summary>
        /// <typeparam name="T">Type of the attributes.</typeparam>
        /// <returns>Enumeration of attributes (possibly empty).</returns>
        public IEnumerable<T> GetTypeCustomAttributes<T>()
        {
            var fromCache = _all.Where( e => e.Attr is T && e.M == Type ).Select( e => (T)e.Attr );
            var fromType = Type.GetCustomAttributes( _includeBaseClasses )
                                .Where( a => !(a is IAttributeContextBound) && a is T ).Select( a => (T)(object)a );
            return fromCache.Concat( fromType );
        }

    }

}

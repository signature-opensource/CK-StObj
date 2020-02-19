using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Implements a cache for attributes associated to a type or to any of its members that
    /// support <see cref="IAttributeContextBound"/>.
    /// Attribute inheritance is ignored: only attributes applied to the member are considered. 
    /// When used with another type or a member of another type from the one provided 
    /// in the constructor, an exception is thrown.
    /// </summary>
    public class TypeAttributesCache : ICKCustomAttributeTypeMultiProvider
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
        /// Initializes a new <see cref="TypeAttributesCache"/> that considers only members explicitly 
        /// declared by the <paramref name="type"/>.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="type">Type for which attributes must be cached.</param>
        /// <param name="services">Available services that will be used for delegated attribute constructor injection.</param>
        /// <param name="includeBaseClasses">True to include attributes of base classes and attributes on members of the base classes.</param>
        public TypeAttributesCache(IActivityMonitor monitor, Type type, IServiceProvider services, bool includeBaseClasses)
        {
            if( type == null ) throw new ArgumentNullException( nameof(type) );
            Type = type;
            var all = new List<Entry>();
            int initializerCount = Register( monitor, services, all, type, includeBaseClasses );
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            if( includeBaseClasses ) flags &= ~BindingFlags.DeclaredOnly;
            _typeMembers = type.GetMembers( flags );
            foreach( var m in _typeMembers ) initializerCount += Register( monitor, services, all, m );
            _all = all.ToArray();
            _includeBaseClasses = includeBaseClasses;
            if( initializerCount > 0 )
            {
                foreach( Entry e in _all )
                {
                    if( e.Attr is IAttributeContextBoundInitializer aM )
                    {
                        aM.Initialize( this, e.M );
                        if( --initializerCount == 0 ) break;
                    }
                }
            }
        }

        static int Register( IActivityMonitor monitor, IServiceProvider services, List<Entry> all, MemberInfo m, bool inherit = false )
        {
            int initializerCount = 0;
            var attr = (IAttributeContextBound[])m.GetCustomAttributes( typeof( IAttributeContextBound ), inherit );
            foreach( var a in attr )
            {
                object finalAttributeToUse = a;
                if( a is ContextBoundDelegationAttribute delegated )
                {
                    Type dT = SimpleTypeFinder.WeakResolver( delegated.ActualAttributeTypeAssemblyQualifiedName, true );
                    finalAttributeToUse = services.SimpleObjectCreate( monitor, dT, a );
                    if( finalAttributeToUse == null ) continue;
                }
                all.Add( new Entry( m, finalAttributeToUse ) );
                if( finalAttributeToUse is IAttributeContextBoundInitializer ) ++initializerCount;
            }
            return initializerCount;
        }

        /// <summary>
        /// Get the Type that is managed by this cache.
        /// </summary>
        public Type Type { get; } 

        /// <summary>
        /// Gets whether an attribute that is assignable to the given <paramref name="attributeType"/> 
        /// exists on the given member.
        /// </summary>
        /// <param name="m">The member.</param>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>True if at least one attribute exists.</returns>
        public bool IsDefined( MemberInfo m, Type attributeType )
        {
            if( m == null ) throw new ArgumentNullException( nameof(m) );
            if( attributeType == null ) throw new ArgumentNullException( nameof(attributeType) );
            return _all.Any( e => CK.Reflection.MemberInfoEqualityComparer.Default.Equals( e.M, m ) 
                                  && attributeType.IsAssignableFrom( e.Attr.GetType() ) )
                    || ( (m.DeclaringType == Type || (_includeBaseClasses && m.DeclaringType.IsAssignableFrom( Type ))) 
                         && m.GetCustomAttributes(false).Any( a => attributeType.IsAssignableFrom( a.GetType()) ) );
        }

        /// <summary>
        /// Gets attributes on a <see cref="MemberInfo"/> that are assignable to <paramref name="attributeType"/>.
        /// Instances of attributes that support <see cref="IAttributeContextBound"/> are always the same. 
        /// Other attributes are instanciated (by calling <see cref="MemberInfo.GetCustomAttributes(Type,bool)"/>).
        /// </summary>
        /// <param name="m">Method of <see cref="P:Type"/>.</param>
        /// <param name="attributeType">Type that must be supported by the attributes.</param>
        /// <returns>A set of attributes that are guaranteed to be assignable to <paramref name="attributeType"/>.</returns>
        public IEnumerable<object> GetCustomAttributes( MemberInfo m, Type attributeType )
        {
            if( m == null ) throw new ArgumentNullException( "m" );
            if( attributeType == null ) throw new ArgumentNullException( "attributeType" );
            var fromCache = _all.Where( e => CK.Reflection.MemberInfoEqualityComparer.Default.Equals( e.M, m ) && attributeType.IsAssignableFrom( e.Attr.GetType() ) ).Select( e => e.Attr );
            if( m.DeclaringType == Type || (_includeBaseClasses && m.DeclaringType.IsAssignableFrom( Type )) )
            {
                return fromCache
                        .Concat( m.GetCustomAttributes( false ).Where( a => !(a is IAttributeContextBound) && attributeType.IsAssignableFrom( a.GetType() ) ) );
            }
            return fromCache;
        }

        /// <summary>
        /// Gets attributes on a <see cref="MemberInfo"/> that are assignable to <typeparamref name="T"/>.
        /// Instances of attributes that support <see cref="IAttributeContextBound"/> are always the same. 
        /// Other attributes are instanciated (by calling <see cref="MemberInfo.GetCustomAttributes(Type,bool)"/>).
        /// </summary>
        /// <typeparam name="T">Type that must be supported by the attributes.</typeparam>
        /// <param name="m">Method of <see cref="P:Type"/>.</param>
        /// <returns>A set of typed attributes.</returns>
        public IEnumerable<T> GetCustomAttributes<T>( MemberInfo m )
        {
            if( m == null ) throw new ArgumentNullException( "m" );
            var fromCache = _all.Where( e => CK.Reflection.MemberInfoEqualityComparer.Default.Equals( e.M, m ) && e.Attr is T ).Select( e => (T)e.Attr );
            if( m.DeclaringType == Type || (_includeBaseClasses && m.DeclaringType.IsAssignableFrom( Type )) )
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

        /// <summary>
        /// Gets all attributes that are assignable to the given <paramref name="attributeType"/>, regardless of the <see cref="MemberInfo"/>
        /// that carries it. 
        /// </summary>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <param name="memberOnly">True to ignore attributes of the type itself.</param>
        /// <returns>Enumeration of attributes (possibly empty).</returns>
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
        /// Gets all <see cref="MemberInfo"/> that this <see cref="ICKCustomAttributeMultiProvider"/> handles.
        /// The <see cref="Type"/> is appended to this list.
        /// </summary>
        /// <returns>Enumeration of members.</returns>
        public IEnumerable<MemberInfo> GetMembers() => _typeMembers.Append( Type );

    }

}

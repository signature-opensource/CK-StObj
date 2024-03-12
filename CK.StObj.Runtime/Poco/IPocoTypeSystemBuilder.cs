using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// The Poco type system manages by default all the types reachable from IPoco
    /// objects. Additional <see cref="IRecordPocoType"/> and <see cref="ICollectionPocoType"/> can
    /// be registered.
    /// </summary>
    public interface IPocoTypeSystemBuilder
    {
        /// <summary>
        /// Gets the low level Poco directory on which this Type System is built.
        /// </summary>
        IPocoDirectory PocoDirectory { get; }

        /// <summary>
        /// Gets the total count of registered types (nullables and non nulables).
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Tries to find by type. Only types that are oblivious (see <see cref="IPocoType.ObliviousType"/>) and IPoco
        /// interfaces can be found by this method.
        /// <para>
        /// Notably, collection abstractions (<c>IList&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>, <c>IDictionary&lt;TKey,TValue&gt;</c> and their IReadOnly)
        /// cannot be found by this method. 
        /// <para>
        /// This can find only the <see cref="IPocoType.IsOblivious"/> types and <see cref="ISecondaryPocoType"/> types.
        /// </para>
        /// </summary>
        /// <param name="type">The type to find.</param>
        /// <returns>The Poco type or null.</returns>
        IPocoType? FindByType( Type type );

        /// <summary>
        /// Tries to find by type. Only types that are oblivious (see <see cref="IPocoType.ObliviousType"/>) and IPoco
        /// interfaces can be found by this method.
        /// <para>
        /// Notably, collection abstractions (<c>IList&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>, <c>IDictionary&lt;TKey,TValue&gt;</c> and their IReadOnly)
        /// cannot be found by this method. 
        /// <para>
        /// This can find only the <see cref="IPocoType.IsOblivious"/> types and <see cref="ISecondaryPocoType"/> types.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The expected <see cref="IPocoType"/>.</typeparam>
        /// <param name="type">The type to find.</param>
        /// <returns>The Poco type or null.</returns>
        T? FindByType<T>( Type type ) where T : class, IPocoType;

        /// <summary>
        /// Finds an open generic definition of <see cref="IAbstractPocoType"/>.
        /// The type must be used by at least one <see cref="IPocoGenericTypeDefinition.Instances"/>.
        /// </summary>
        /// <param name="type">Type to find. Must be an open generic type (<c>typeof( ICommand<> )</c>).</param>
        /// <returns>The type definition or null.</returns>
        IPocoGenericTypeDefinition? FindGenericTypeDefinition( Type type );

        /// <summary>
        /// Locks this type system: no more registration can be done,
        /// <see cref="SetNotExchangeable(IActivityMonitor, IPocoType)"/> cannot be called anymore.
        /// <para>
        /// This can be called multiple times.
        /// </para>
        /// <para>
        /// This may throw a <see cref="CKException"/> if a [<see cref="RegisterPocoTypeAttribute"/>] type
        /// registration failed.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The built type system.</returns>
        IPocoTypeSystem Lock( IActivityMonitor monitor );

        /// <summary>
        /// Gets whether this type system has been locked.
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Gets the set of types that must be generated to support this type system.
        /// </summary>
        IReadOnlyCollection<IPocoRequiredSupportType> RequiredSupportTypes { get; }

        /// <summary>
        /// Forbids a type to be exchangeable. This is the same as using the <see cref="NotExchangeableAttribute"/>
        /// on the type except that this can be called on anonymous records and even basic types.
        /// </para>
        /// <para>
        /// If <paramref name="type"/> is a <see cref="PocoTypeKind.SecondaryPoco"/> this makes its <see cref="IPrimaryPocoType"/>
        /// not exchangeable. Similarly, a <see cref="IAbstractPocoType"/> applies to all its <see cref="IAbstractPocoType.PrimaryPocoTypes"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">Required monitor.</param>
        /// <param name="type">The type that must not be exchangeable.</param>
        void SetNotExchangeable( IActivityMonitor monitor, IPocoType type );

        /// <summary>
        /// Forbids a type to be serializable. This is the same as using the <see cref="NotSerializableAttribute"/>
        /// on the type except that this can be called with anonymous records and even basic types.
        /// </para>
        /// <para>
        /// If <paramref name="type"/> is a <see cref="PocoTypeKind.SecondaryPoco"/> this makes its <see cref="IPrimaryPocoType"/>
        /// not serializable. Similarly, a <see cref="IAbstractPocoType"/> applies to all its <see cref="IAbstractPocoType.PrimaryPocoTypes"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">Required monitor.</param>
        /// <param name="type">The type that must be non serializable.</param>
        void SetNotSerializable( IActivityMonitor monitor, IPocoType type );

        /// <summary>
        /// Tries to register a type.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="memberInfo">The <see cref="IExtMemberInfo"/> whose <see cref="IExtMemberInfo.Type"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        IPocoType? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo );

        /// <summary>
        /// Tries to register a new type through a PropertyInfo (this is required for
        /// nullability analysis).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The PropertyInfo whose <see cref="PropertyInfo.PropertyType"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        IPocoType? Register( IActivityMonitor monitor, PropertyInfo p );

        /// <summary>
        /// Tries to register a new type through a FieldInfo (this is required for
        /// nullability analysis).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="f">The FieldInfo whose <see cref="FieldInfo.FieldType"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        IPocoType? Register( IActivityMonitor monitor, FieldInfo f );

        /// <summary>
        /// Tries to register a new type through a ParameterInfo (this is required for
        /// nullability analysis).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="f">The ParameterInfo whose <see cref="ParameterInfo.ParameterType"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        IPocoType? Register( IActivityMonitor monitor, ParameterInfo f );

        /// <summary>
        /// Tries to register a new type. On success, the obtained type is always non nullable for reference type
        /// and all subordinated reference types are non nullable.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">The type that must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        IPocoType? RegisterNullOblivious( IActivityMonitor monitor, Type t );
    }

}

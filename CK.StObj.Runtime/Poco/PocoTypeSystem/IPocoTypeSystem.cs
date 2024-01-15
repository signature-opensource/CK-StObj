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
    public interface IPocoTypeSystem
    {
        /// <summary>
        /// Gets the "object" (<see cref="PocoTypeKind.Any"/>) type.
        /// </summary>
        IPocoType ObjectType { get; }

        /// <summary>
        /// Gets all the registered types by their <see cref="IPocoType.Index"/>.
        /// This contains both nullable and non nullable types.
        /// </summary>
        IReadOnlyList<IPocoType> AllTypes { get; }

        /// <summary>
        /// Gets all the registered non nullable types.
        /// </summary>
        IReadOnlyList<IPocoType> AllNonNullableTypes { get; }

        /// <summary>
        /// Locks this type system: no more registration can be done,
        /// <see cref="SetNotExchangeable(IActivityMonitor, IPocoType)"/> cannot be called anymore.
        /// </summary>
        /// <param name="monitor"></param>
        void Lock( IActivityMonitor monitor );

        /// <summary>
        /// Gets whether this type system has been locked.
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Gets the set of types that must be generated to support this type system.
        /// </summary>
        IReadOnlyCollection<PocoRequiredSupportType> RequiredSupportTypes { get; }

        /// <summary>
        /// Tries to find by type. Not all types can be indexed by types: the most obvious are nullable reference types
        /// but collection abstractions (<c>IList&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>, <c>IDictionary&lt;TKey,TValue&gt;</c>)
        /// are not. Only types that are oblivious (see <see cref="IPocoType.ObliviousType"/>) and IPoco
        /// interfaces can be found by this method.
        /// </summary>
        /// <param name="type">The type to find.</param>
        /// <returns>The Poco type or null.</returns>
        IPocoType? FindByType( Type type );

        /// <summary>
        /// Tries to find by type. Not all types can be indexed by types: the most obvious are nullable reference types
        /// but collection abstractions (<c>IList&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>, <c>IDictionary&lt;TKey,TValue&gt;</c>)
        /// are not. Only types that are oblivious (see <see cref="IPocoType.ObliviousType"/>) and IPoco
        /// interfaces can be found by this method.
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
        /// Forbids a type to be <see cref="IPocoType.IsExchangeable"/>. This
        /// condemns all fields that depend on it to be no more <see cref="IPocoField.IsExchangeable"/>
        /// and can subsequently also condemn other types if all their fields become not exchangeable.
        /// <para>
        /// The "object" (<see cref="PocoTypeKind.Any"/>) is necessarily exchangeable: an <see cref="ArgumentException"/>
        /// is thrown if <paramref name="type"/> is Any. 
        /// </para>
        /// <para>
        /// An argument exception is also thrown if <paramref name="type"/> is a <see cref="PocoTypeKind.SecondaryPoco"/>:
        /// only the <see cref="IPrimaryPocoType"/> or <see cref="IAbstractPocoType"/> can be set to not exchangeable.
        /// </para>
        /// </summary>
        /// <param name="monitor">Required monitor.</param>
        /// <param name="type">The type to condemn.</param>
        void SetNotExchangeable( IActivityMonitor monitor, IPocoType type );

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

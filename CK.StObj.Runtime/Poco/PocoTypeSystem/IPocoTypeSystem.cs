using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// The Poco type system manages by default all the types reachable from IPoco
    /// objects. Further <see cref="IRecordPocoType"/> and <see cref="ICollectionPocoType"/> can
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
        /// Gets the set of types that must be generated to support this type system.
        /// </summary>
        IReadOnlyCollection<PocoRequiredSupportType> RequiredSupportTypes { get; }

        /// <summary>
        /// Tries to find a Poco type from an actual type.
        /// Anonymous <see cref="IRecordPocoType"/> cannot be found by this method.
        /// When the <paramref name="type"/> is a reference type, its non nullable
        /// Poco type is returned.
        /// </summary>
        /// <param name="type">The type to find.</param>
        /// <returns>The Poco type or null.</returns>
        IPocoType? FindObliviousType( Type type );
        
        /// <summary>
        /// Gets the primary poco type from any of its interface.
        /// </summary>
        /// <param name="i">The IPoco interface.</param>
        /// <returns>The primary poco type or null.</returns>
        IPrimaryPocoType? GetPrimaryPocoType( Type i );

        /// <summary>
        /// Forbids a type to be <see cref="IPocoType.IsExchangeable"/>. This
        /// condemns all fields that depend on it to be no more <see cref="IPocoField.IsExchangeable"/>
        /// and can subsequently also condemn other types if all their fields become not exchangeable.
        /// <para>
        /// The "object" (<see cref="PocoTypeKind.Any"/>) is necessarily exchangeable. 
        /// </para>
        /// </summary>
        /// <param name="monitor">Required monitor.</param>
        /// <param name="type">The type to condemn.</param>
        void SetNotExchangeable( IActivityMonitor monitor, IPocoType type );

        /// <summary>
        /// Tries to register a type.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The <see cref="IExtMemberInfo"/> whose <see cref="IExtMemberInfo.Type"/> must be registered.</param>
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
        /// Tries to register a new type. On success, the obtained type is the
        /// always nullable for reference type and all subordinated reference types are nullable.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">The type that must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        IPocoType? RegisterNullableOblivious( IActivityMonitor monitor, Type t );
    }

}
